// This file is part of YouTubeMusicStreamer.
// Copyright (C) 2026 Dominic Ris
// 
// YouTubeMusicStreamer is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as published
// by the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// YouTubeMusicStreamer is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU Affero General Public License for more details.
// 
// For full license text, see the LICENSE file in the project’s root directory.
// 
// You should have received a copy of the GNU Affero General Public License
// along with YouTubeMusicStreamer. If not, see <https://www.gnu.org/licenses/>.

using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NewtonsoftJsonException = Newtonsoft.Json.JsonException;
using XeroxDev.YTMDesktop.Companion.Enums;
using XeroxDev.YTMDesktop.Companion.Exceptions;
using XeroxDev.YTMDesktop.Companion.Models.Output;
using XeroxDev.YTMDesktop.Companion.Settings;
using YouTubeMusicStreamer.Services.App;

namespace YouTubeMusicStreamer.Services.YouTube;

public sealed class YtmCompanionSessionCoordinator(
    ILogger<YtmCompanionSessionCoordinator> logger,
    SettingsService settingsService,
    IAppIdentitySource appIdentitySource,
    IAppVersionSource appVersionSource,
    IYtmCompanionConnectorFactory connectorFactory)
    : IYtmCompanionSessionCoordinator, IDisposable
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RecoveryLogThrottle = TimeSpan.FromSeconds(30);
    private const string RecoveryUnavailableMessage = "YTMDesktop companion is unreachable. Reconnecting automatically.";

    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private ConnectorSettings? _connectorSettings;
    private IYtmCompanionConnector? _companionConnector;
    private CancellationTokenSource? _retryCts;
    private Task? _retryTask;
    private DateTimeOffset? _lastRecoveryInfoLoggedAt;
    private bool _hadHealthyConnection;
    private bool _disposed;

    public IYtmRestClient? RestClient => _companionConnector?.RestClient;
    public IYtmSocketClient? SocketClient => _companionConnector?.SocketClient;

    public YtmCompanionSessionState State { get; private set; } = new(
        new YtmEndpointConfiguration(null, null),
        YtmEndpointStatus.Unknown,
        YtmAuthorizationStatus.NotConfigured,
        YtmConnectionStatus.Disconnected,
        ESocketState.Disconnected,
        null,
        null,
        false,
        null);

    public event EventHandler<YtmCompanionSessionState> StateChanged = delegate { };
    public event EventHandler<StateOutput> PlaybackStateChanged = delegate { };
    public event EventHandler<ESocketState> SocketConnectionChanged = delegate { };
    public event EventHandler<PlaylistOutput> PlaylistCreated = delegate { };
    public event EventHandler<string> PlaylistDeleted = delegate { };
    public event EventHandler<Exception> ErrorOccurred = delegate { };

    public async Task InitializeIfNeededAsync(CancellationToken cancellationToken = default)
    {
        await EnsureConnectorSettingsAsync(cancellationToken);

        var youTubeSettings = settingsService.GetYouTubeSettings();
        var hasConfiguredEndpoint = !string.IsNullOrWhiteSpace(youTubeSettings.Host) && youTubeSettings.Port is > 0 and <= 65535;
        var hasToken = !string.IsNullOrWhiteSpace(_connectorSettings?.Token);

        if (!hasConfiguredEndpoint)
        {
            UpdateState(State with
            {
                Endpoint = new YtmEndpointConfiguration(null, null),
                EndpointStatus = YtmEndpointStatus.NotConfigured,
                AuthorizationStatus = YtmAuthorizationStatus.NotConfigured,
                ConnectionStatus = YtmConnectionStatus.Disconnected,
                SocketState = ESocketState.Disconnected,
                ErrorMessage = null,
                AuthorizationCode = null,
                HasStoredToken = hasToken
            });
            return;
        }

        if (!hasToken)
        {
            UpdateState(State with
            {
                Endpoint = new YtmEndpointConfiguration(_connectorSettings!.Host, _connectorSettings.Port),
                EndpointStatus = YtmEndpointStatus.Unknown,
                AuthorizationStatus = YtmAuthorizationStatus.AuthRequired,
                ConnectionStatus = YtmConnectionStatus.Disconnected,
                SocketState = ESocketState.Disconnected,
                ErrorMessage = null,
                AuthorizationCode = null,
                HasStoredToken = false
            });
            return;
        }

        await ConnectInternalAsync(forceNewToken: false, allowAuthFlow: false, retryOnUnavailable: true, cancellationToken);
    }

    public async Task ConfigureEndpointAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        await EnsureConnectorSettingsAsync(cancellationToken);

        var normalizedHost = GetValidatedHost(host);
        var normalizedPort = GetValidatedPort(port);
        var endpointChanged = !string.Equals(_connectorSettings!.Host, normalizedHost, StringComparison.OrdinalIgnoreCase) ||
                              _connectorSettings.Port != normalizedPort;

        _connectorSettings.Host = normalizedHost;
        _connectorSettings.Port = normalizedPort;

        await settingsService.SaveYouTubeSettingsAsync(new Services.App.Persistence.YouTubeSettingsSnapshot(
            normalizedHost,
            normalizedPort,
            settingsService.GetYouTubeSettings().AutoStartServer,
            settingsService.GetYouTubeSettings().PublicPort,
            settingsService.GetYouTubeSettings().AllowAudioCapture,
            settingsService.GetYouTubeSettings().AudioCaptureDevice));

        if (endpointChanged)
        {
            _connectorSettings.Token = null;
            await settingsService.SaveSensitiveSettingAsync(s => s.YtmDesktopToken = null);
            _companionConnector?.SetAuthToken(null);
        }

        await ConnectInternalAsync(forceNewToken: endpointChanged || string.IsNullOrWhiteSpace(_connectorSettings.Token), allowAuthFlow: true, retryOnUnavailable: false, cancellationToken);
    }

    public async Task ReconnectAsync(CancellationToken cancellationToken = default)
    {
        await EnsureConnectorSettingsAsync(cancellationToken);
        CancelRetryLoop();

        _connectorSettings!.Token = null;
        await settingsService.SaveSensitiveSettingAsync(s => s.YtmDesktopToken = null);
        _companionConnector?.SetAuthToken(null);
        _hadHealthyConnection = false;

        await ConnectInternalAsync(
            forceNewToken: true,
            allowAuthFlow: true,
            retryOnUnavailable: false,
            cancellationToken);
    }

    private async Task ConnectInternalAsync(bool forceNewToken, bool allowAuthFlow, bool retryOnUnavailable, CancellationToken cancellationToken)
    {
        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            CancelRetryLoop();
            await EnsureConnectorSettingsAsync(cancellationToken);
            EnsureConnector();

            UpdateState(State with
            {
                Endpoint = new YtmEndpointConfiguration(_connectorSettings!.Host, _connectorSettings.Port),
                EndpointStatus = retryOnUnavailable && State.EndpointStatus != YtmEndpointStatus.Unknown
                    ? State.EndpointStatus
                    : YtmEndpointStatus.Unknown,
                AuthorizationStatus = string.IsNullOrWhiteSpace(_connectorSettings.Token)
                    ? YtmAuthorizationStatus.AuthRequired
                    : YtmAuthorizationStatus.Authorized,
                ConnectionStatus = retryOnUnavailable && State.ConnectionStatus is YtmConnectionStatus.Retrying or YtmConnectionStatus.Degraded
                    ? YtmConnectionStatus.Retrying
                    : YtmConnectionStatus.Connecting,
                ErrorMessage = retryOnUnavailable ? State.ErrorMessage : null,
                AuthorizationCode = null,
                HasStoredToken = !string.IsNullOrWhiteSpace(_connectorSettings.Token)
            });

            var probeResult = await ProbeEndpointAsync(cancellationToken);
            if (probeResult != YtmEndpointStatus.ValidCompanion)
            {
                var recoveringSession = retryOnUnavailable && !string.IsNullOrWhiteSpace(_connectorSettings?.Token);
                var failureState = State with
                {
                    EndpointStatus = probeResult,
                    AuthorizationStatus = string.IsNullOrWhiteSpace(_connectorSettings?.Token)
                        ? YtmAuthorizationStatus.AuthRequired
                        : State.AuthorizationStatus,
                    ConnectionStatus = probeResult == YtmEndpointStatus.Unreachable
                        ? recoveringSession ? YtmConnectionStatus.Retrying : YtmConnectionStatus.Degraded
                        : YtmConnectionStatus.Error,
                    SocketState = ESocketState.Disconnected,
                    ErrorMessage = probeResult switch
                    {
                        YtmEndpointStatus.Unreachable => recoveringSession ? RecoveryUnavailableMessage : "YTMDesktop companion is unreachable.",
                        YtmEndpointStatus.IncompatibleEndpoint => "The configured endpoint is not a compatible YTMDesktop companion.",
                        _ => null
                    },
                    AuthorizationCode = null
                };

                UpdateState(failureState);

                if (retryOnUnavailable && probeResult == YtmEndpointStatus.Unreachable && !string.IsNullOrWhiteSpace(_connectorSettings?.Token))
                    ScheduleRetry(RecoveryUnavailableMessage);

                return;
            }

            UpdateState(State with { EndpointStatus = YtmEndpointStatus.ValidCompanion });

            if (forceNewToken || string.IsNullOrWhiteSpace(_connectorSettings.Token))
            {
                if (!allowAuthFlow)
                {
                    UpdateState(State with
                    {
                        AuthorizationStatus = YtmAuthorizationStatus.AuthRequired,
                        ConnectionStatus = YtmConnectionStatus.Disconnected,
                        ErrorMessage = "YTMDesktop authorization is required.",
                        AuthorizationCode = null,
                        HasStoredToken = false
                    });
                    return;
                }

                var authorizationSucceeded = await AcquireTokenAsync(cancellationToken);
                if (!authorizationSucceeded)
                    return;
            }

            UpdateState(State with
            {
                AuthorizationStatus = YtmAuthorizationStatus.Authorized,
                ConnectionStatus = YtmConnectionStatus.Connecting,
                ErrorMessage = null,
                AuthorizationCode = null,
                HasStoredToken = true
            });

            await SocketClient!.ConnectAsync();
            _hadHealthyConnection = true;

            UpdateState(State with
            {
                EndpointStatus = YtmEndpointStatus.ValidCompanion,
                ConnectionStatus = YtmConnectionStatus.Connected,
                SocketState = ESocketState.Connected,
                ErrorMessage = null,
                AuthorizationCode = null
            });

            logger.LogInformation("Connected to YTMDesktop companion");
        }
        catch (Exception ex)
        {
            var userMessage = DescribeUserFacingError(ex);
            var recoveringSession = retryOnUnavailable && !string.IsNullOrWhiteSpace(_connectorSettings?.Token);
            var isUnreachable = IsUnreachableException(ex);

            if (recoveringSession && isUnreachable)
                LogRecoveryInfoThrottled("YTMDesktop companion is unreachable during retry.");
            else
                logger.LogWarning(ex, "Failed to connect to the YTMDesktop companion");

            UpdateState(State with
            {
                ConnectionStatus = recoveringSession
                    ? YtmConnectionStatus.Retrying
                    : YtmConnectionStatus.Error,
                SocketState = ESocketState.Disconnected,
                ErrorMessage = recoveringSession && isUnreachable ? RecoveryUnavailableMessage : userMessage
            });

            if (recoveringSession)
                ScheduleRetry(RecoveryUnavailableMessage);
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private async Task<bool> AcquireTokenAsync(CancellationToken cancellationToken)
    {
        try
        {
            UpdateState(State with
            {
                AuthorizationStatus = YtmAuthorizationStatus.Authorizing,
                ConnectionStatus = YtmConnectionStatus.Disconnected,
                ErrorMessage = null,
                AuthorizationCode = null
            });

            var code = await RestClient!.GetAuthCodeAsync();
            if (string.IsNullOrWhiteSpace(code))
            {
                UpdateState(State with
                {
                    AuthorizationStatus = YtmAuthorizationStatus.AuthFailed,
                    ConnectionStatus = YtmConnectionStatus.Error,
                    ErrorMessage = "Couldn't get authorization code from YTMDesktop.",
                    AuthorizationCode = null
                });
                return false;
            }

            UpdateState(State with { AuthorizationCode = code });

            var token = await RestClient.GetAuthTokenAsync(code);
            if (string.IsNullOrWhiteSpace(token))
            {
                UpdateState(State with
                {
                    AuthorizationStatus = YtmAuthorizationStatus.AuthFailed,
                    ConnectionStatus = YtmConnectionStatus.Error,
                    ErrorMessage = "Couldn't get authorization token from YTMDesktop.",
                    AuthorizationCode = code,
                    HasStoredToken = false
                });
                return false;
            }

            _connectorSettings!.Token = token;
            _companionConnector!.SetAuthToken(token);
            await settingsService.SaveSensitiveSettingAsync(s => s.YtmDesktopToken = token);

            UpdateState(State with
            {
                AuthorizationStatus = YtmAuthorizationStatus.Authorized,
                ErrorMessage = null,
                AuthorizationCode = null,
                HasStoredToken = true
            });

            logger.LogInformation("YTMDesktop authorization token saved successfully");
            return true;
        }
        catch (Exception ex)
        {
            var userMessage = DescribeUserFacingError(ex);
            logger.LogWarning(ex, "Failed to authorize against YTMDesktop");

            UpdateState(State with
            {
                AuthorizationStatus = YtmAuthorizationStatus.AuthFailed,
                ConnectionStatus = YtmConnectionStatus.Error,
                ErrorMessage = userMessage,
                HasStoredToken = false
            });
            return false;
        }
    }

    private async Task<YtmEndpointStatus> ProbeEndpointAsync(CancellationToken cancellationToken)
    {
        try
        {
            var metadata = await RestClient!.GetMetadataAsync();
            return metadata is null ? YtmEndpointStatus.IncompatibleEndpoint : YtmEndpointStatus.ValidCompanion;
        }
        catch (Exception ex) when (IsUnreachableException(ex))
        {
            LogRecoveryInfoThrottled("YTMDesktop endpoint probe classified the endpoint as unreachable");
            return YtmEndpointStatus.Unreachable;
        }
        catch (ApiException ex)
        {
            if (IsUnreachableException(ex))
            {
                LogRecoveryInfoThrottled("YTMDesktop endpoint probe classified the endpoint as unreachable");
                return YtmEndpointStatus.Unreachable;
            }

            logger.LogInformation(ex, "YTMDesktop endpoint probe classified the endpoint as incompatible");
            return YtmEndpointStatus.IncompatibleEndpoint;
        }
        catch (JsonException ex)
        {
            logger.LogInformation(ex, "YTMDesktop endpoint probe received an incompatible response");
            return YtmEndpointStatus.IncompatibleEndpoint;
        }
        catch (NewtonsoftJsonException ex)
        {
            logger.LogInformation(ex, "YTMDesktop endpoint probe received an incompatible response");
            return YtmEndpointStatus.IncompatibleEndpoint;
        }
    }

    private void EnsureConnector()
    {
        if (_companionConnector is not null)
            return;

        _companionConnector = connectorFactory.Create(_connectorSettings!);
        SocketClient!.Error += HandleSocketError;
        SocketClient.ConnectionChanged += HandleSocketConnectionChange;
        SocketClient.PlaybackStateChanged += HandleSocketStateChange;
        SocketClient.PlaylistCreated += HandlePlaylistCreated;
        SocketClient.PlaylistDeleted += HandlePlaylistDeleted;
    }

    private async Task EnsureConnectorSettingsAsync(CancellationToken cancellationToken)
    {
        if (_connectorSettings is not null)
            return;

        var sensitiveSettings = await settingsService.GetSensitiveSettingsAsync();
        _connectorSettings = new ConnectorSettings(
            GetValidatedHost(),
            GetValidatedPort(),
            appIdentitySource.AppId,
            appIdentitySource.AppName,
            appVersionSource.GetInternalAppVersion().ToNormalizedString(),
            sensitiveSettings.YtmDesktopToken);
    }

    private void HandleSocketError(object? sender, Exception exception)
    {
        var isUnreachable = IsUnreachableException(exception);
        var userMessage = DescribeUserFacingError(exception);
        var recoveringSession = _hadHealthyConnection && !string.IsNullOrWhiteSpace(_connectorSettings?.Token);

        if (recoveringSession && isUnreachable)
            LogRecoveryInfoThrottled("YTMDesktop companion socket is unreachable during recovery.");
        else
            logger.LogWarning(exception, "YTMDesktop companion socket reported an error");

        ErrorOccurred(this, new Exception(userMessage, exception));
        UpdateState(State with
        {
            EndpointStatus = isUnreachable ? YtmEndpointStatus.Unreachable : State.EndpointStatus,
            ConnectionStatus = recoveringSession ? YtmConnectionStatus.Retrying : YtmConnectionStatus.Degraded,
            ErrorMessage = recoveringSession && isUnreachable ? RecoveryUnavailableMessage : userMessage
        });
    }

    private void HandleSocketConnectionChange(object? sender, ESocketState socketState)
    {
        SocketConnectionChanged(this, socketState);

        if (socketState == ESocketState.Connected)
            CancelRetryLoop();

        var recoveringSession = _hadHealthyConnection && !string.IsNullOrWhiteSpace(_connectorSettings?.Token);
        var mappedStatus = socketState switch
        {
            ESocketState.Connecting when recoveringSession => YtmConnectionStatus.Retrying,
            ESocketState.Connecting => YtmConnectionStatus.Connecting,
            ESocketState.Connected => YtmConnectionStatus.Connected,
            _ when State.ConnectionStatus == YtmConnectionStatus.Retrying => YtmConnectionStatus.Retrying,
            _ when recoveringSession => YtmConnectionStatus.Retrying,
            _ => YtmConnectionStatus.Disconnected
        };

        UpdateState(State with
        {
            SocketState = socketState,
            ConnectionStatus = mappedStatus
        });

        if (socketState == ESocketState.Disconnected && _hadHealthyConnection && !string.IsNullOrWhiteSpace(_connectorSettings?.Token))
            ScheduleRetry("YTMDesktop socket disconnected. Reconnecting automatically.");
    }

    private void HandleSocketStateChange(object? sender, StateOutput state)
    {
        CancelRetryLoop();

        UpdateState(State with
        {
            EndpointStatus = YtmEndpointStatus.ValidCompanion,
            PlaybackSnapshot = new YtmPlaybackSnapshot(state, DateTimeOffset.UtcNow),
            ConnectionStatus = YtmConnectionStatus.Connected,
            SocketState = ESocketState.Connected,
            ErrorMessage = null
        });

        PlaybackStateChanged(this, state);
    }

    private void HandlePlaylistCreated(object? sender, PlaylistOutput playlist) => PlaylistCreated(this, playlist);

    private void HandlePlaylistDeleted(object? sender, string playlistId) => PlaylistDeleted(this, playlistId);

    private void ScheduleRetry(string message)
    {
        if (_retryTask is { IsCompleted: false })
            return;

        _retryCts?.Dispose();
        _retryCts = new CancellationTokenSource();

        UpdateState(State with
        {
            ConnectionStatus = YtmConnectionStatus.Retrying,
            ErrorMessage = message
        });

        _retryTask = Task.Run(async () =>
        {
            while (!_retryCts.IsCancellationRequested && !_disposed)
            {
                try
                {
                    await Task.Delay(RetryDelay, _retryCts.Token);
                    await ConnectInternalAsync(forceNewToken: false, allowAuthFlow: false, retryOnUnavailable: true, _retryCts.Token);
                    return;
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }, _retryCts.Token);
    }

    private void CancelRetryLoop()
    {
        if (_retryCts is null)
            return;

        try
        {
            _retryCts.Cancel();
        }
        catch
        {
            // ignored
        }

        _retryCts.Dispose();
        _retryCts = null;
        _retryTask = null;
    }

    private void UpdateState(YtmCompanionSessionState nextState)
    {
        State = nextState;
        StateChanged(this, nextState);
    }

    private void LogRecoveryInfoThrottled(string message)
    {
        var now = DateTimeOffset.UtcNow;
        if (_lastRecoveryInfoLoggedAt is { } lastLoggedAt && now - lastLoggedAt < RecoveryLogThrottle)
            return;

        _lastRecoveryInfoLoggedAt = now;
        logger.LogInformation(message);
    }

    private static bool IsUnreachableException(Exception ex)
    {
        for (Exception? current = ex; current is not null; current = current.InnerException)
        {
            if (current is HttpRequestException or TaskCanceledException or TimeoutException or SocketException)
                return true;

            var message = current.Message;
            if (message.Contains("Could not connect to 'ws://", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("transport=websocket", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("connection refused", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("actively refused", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("timed out", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string DescribeUserFacingError(Exception ex)
    {
        if (ex is ApiException apiException)
        {
            var apiError = apiException.Error;
            var code = apiError?.Code;
            var message = apiError?.Message;
            var title = apiError?.Error;

            if (!string.IsNullOrWhiteSpace(code))
            {
                if (code.Equals("AUTHORIZATION_DISABLED", StringComparison.OrdinalIgnoreCase))
                    return "YTMDesktop authorization requests are disabled. Enable them in YTMDesktop and try again.";
            }

            if (!string.IsNullOrWhiteSpace(message))
            {
                if (message.Contains("authorization requests are disabled", StringComparison.OrdinalIgnoreCase))
                    return "YTMDesktop authorization requests are disabled. Enable them in YTMDesktop and try again.";

                if (message.Contains("unauthorized", StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrWhiteSpace(title) && title.Contains("unauthorized", StringComparison.OrdinalIgnoreCase)))
                    return "YTMDesktop rejected the authorization request.";

                return $"YTMDesktop connection failed: {message}";
            }

            return "YTMDesktop connection failed due to an unexpected API error.";
        }

        if (IsUnreachableException(ex))
            return "YTMDesktop companion is unreachable.";

        return string.IsNullOrWhiteSpace(ex.Message)
            ? "YTMDesktop connection failed."
            : $"YTMDesktop connection failed: {ex.Message}";
    }

    private string GetValidatedHost(string? host = null)
    {
        host ??= settingsService.GetYouTubeSettings().Host ?? "127.0.0.1";
        return host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ? "127.0.0.1" : host;
    }

    private int GetValidatedPort(int? port = null)
    {
        port ??= settingsService.GetYouTubeSettings().Port ?? 9863;
        return port is <= 0 or > 65535 ? 9863 : port.Value;
    }

    public void Dispose()
    {
        _disposed = true;
        CancelRetryLoop();
        _connectionLock.Dispose();
        GC.SuppressFinalize(this);
    }
}
