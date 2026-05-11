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

using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using XeroxDev.YTMDesktop.Companion.Enums;
using XeroxDev.YTMDesktop.Companion.Models.Output;
using YouTubeMusicStreamer.Enums;
using YouTubeMusicStreamer.Services.App;
using YouTubeMusicStreamer.Services.App.Diagnostics;
using YouTubeMusicStreamer.Services.WebSocket;

namespace YouTubeMusicStreamer.Services.YouTube;

public sealed partial class YouTubeService : IYouTubeStatusSource, IDisposable
{
    private readonly ILogger<YouTubeService> _logger;
    private readonly SettingsService _settings;
    private readonly WebSocketService _webSocketService;
    private readonly SongQueueService _songQueueService;
    private readonly IYtmCompanionSessionCoordinator _sessionCoordinator;
    private readonly IAppDiagnosticsService _diagnosticsService;
    private bool _disposed;

    public IYtmRestClient? RestClient => _sessionCoordinator.RestClient;
    public IYtmSocketClient? SocketClient => _sessionCoordinator.SocketClient;
    public YtmCompanionSessionState SessionState => _sessionCoordinator.State;
    public YtmPlaybackSnapshot? LastPlaybackSnapshot => _sessionCoordinator.State.PlaybackSnapshot;

    private ConnectorState _state = ConnectorState.LoggedOut;
    public ConnectorState State
    {
        get => _state;
        private set => SetField(ref _state, value);
    }

    private string _error = string.Empty;
    public string Error
    {
        get => _error;
        private set => SetField(ref _error, value);
    }

    private ESocketState _connectionState = ESocketState.Disconnected;
    public ESocketState ConnectionState
    {
        get => _connectionState;
        private set => SetField(ref _connectionState, value);
    }

    private string _code = string.Empty;
    public string Code
    {
        get => _code;
        private set => SetField(ref _code, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<ESocketState> OnConnectionChange = delegate { };
    public event EventHandler<StateOutput> OnStateChange = delegate { };
    public event EventHandler<PlaylistOutput> OnPlaylistCreated = delegate { };
    public event EventHandler<string> OnPlaylistDeleted = delegate { };

    public YouTubeService(
        ILogger<YouTubeService> logger,
        SettingsService settings,
        WebSocketService webSocketService,
        SongQueueService songQueueService,
        IYtmCompanionSessionCoordinator sessionCoordinator,
        IAppDiagnosticsService diagnosticsService)
    {
        _logger = logger;
        _settings = settings;
        _webSocketService = webSocketService;
        _songQueueService = songQueueService;
        _sessionCoordinator = sessionCoordinator;
        _diagnosticsService = diagnosticsService;

        _sessionCoordinator.StateChanged += HandleSessionStateChanged;
        _sessionCoordinator.SocketConnectionChanged += HandleSocketConnectionChanged;
        _sessionCoordinator.PlaybackStateChanged += HandlePlaybackStateChanged;
        _sessionCoordinator.PlaylistCreated += HandlePlaylistCreated;
        _sessionCoordinator.PlaylistDeleted += HandlePlaylistDeleted;
        _sessionCoordinator.ErrorOccurred += HandleErrorOccurred;

        ApplySessionState(_sessionCoordinator.State);
    }

    public async Task InitializeIfNeededAsync() => await _sessionCoordinator.InitializeIfNeededAsync();

    public async Task SetAddressAsync(string host, int port) => await _sessionCoordinator.ConfigureEndpointAsync(host, port);

    public async Task ReconnectAsync() => await _sessionCoordinator.ReconnectAsync();

    public string GetValidatedHost(string? host = null)
    {
        host ??= _settings.GetYouTubeSettings().Host ?? "127.0.0.1";
        return host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ? "127.0.0.1" : host;
    }

    public int GetValidatedPort(int? port = null)
    {
        port ??= _settings.GetYouTubeSettings().Port ?? 9863;
        return port is <= 0 or > 65535 ? 9863 : port.Value;
    }

    private void HandleSessionStateChanged(object? sender, YtmCompanionSessionState state) => ApplySessionState(state);

    private void HandleSocketConnectionChanged(object? sender, ESocketState socketState)
    {
        ConnectionState = socketState;
        OnConnectionChange(this, socketState);
    }

    private void HandlePlaybackStateChanged(object? sender, StateOutput state)
    {
        OnStateChange(this, state);

        _ = Task.Run(async () =>
        {
            try
            {
                await _songQueueService.YouTubeStateChanged(state);

                if (_webSocketService.IsRunning)
                    await _webSocketService.BroadcastTrackInfoAsync(state);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to process a YTMDesktop playback state update");
            }
        });
    }

    private void HandlePlaylistCreated(object? sender, PlaylistOutput playlist) => OnPlaylistCreated(this, playlist);

    private void HandlePlaylistDeleted(object? sender, string playlistId) => OnPlaylistDeleted(this, playlistId);

    private void HandleErrorOccurred(object? sender, Exception exception)
    {
        var message = exception.Message;
        var isRecoveryConnectivityIssue = message.Contains("companion is unreachable", StringComparison.OrdinalIgnoreCase);
        var isAuthIssue = message.Contains("auth", StringComparison.OrdinalIgnoreCase) ||
                          message.Contains("authorization", StringComparison.OrdinalIgnoreCase);

        var reporter = _logger.Diagnostic(_diagnosticsService, AppDiagnosticSubsystem.YouTube)
            .WithDetail(Error)
            .WithException(exception)
            .Visibility(isRecoveryConnectivityIssue || isAuthIssue
                ? AppDiagnosticVisibility.StatusOnly
                : AppDiagnosticVisibility.Toast);

        if (isAuthIssue)
        {
            reporter.Warning(AppDiagnosticCategory.Auth, message).Write();
            return;
        }

        if (isRecoveryConnectivityIssue)
        {
            reporter.Warning(AppDiagnosticCategory.Connectivity, message).Write();
            return;
        }

        reporter.Error(AppDiagnosticCategory.Connectivity, message).Write();
    }

    private void ApplySessionState(YtmCompanionSessionState state)
    {
        ConnectionState = state.SocketState;
        Code = state.AuthorizationCode ?? string.Empty;
        Error = state.ErrorMessage ?? string.Empty;
        State = state.AuthorizationStatus == YtmAuthorizationStatus.Authorizing
            ? ConnectorState.LoggingIn
            : state.ConnectionStatus switch
            {
                YtmConnectionStatus.Connected => ConnectorState.LoggedIn,
                YtmConnectionStatus.Connecting or YtmConnectionStatus.Retrying => ConnectorState.Loading,
                YtmConnectionStatus.Error => ConnectorState.Error,
                YtmConnectionStatus.Degraded when state.AuthorizationStatus == YtmAuthorizationStatus.Authorized => ConnectorState.Error,
                _ when state.AuthorizationStatus is YtmAuthorizationStatus.AuthRequired or YtmAuthorizationStatus.NotConfigured => ConnectorState.LoggedOut,
                _ => ConnectorState.Loading
            };
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;

        field = value;
        OnPropertyChanged(propertyName);
    }

    public static string? GetVideoId(string url)
        => YouTubeUrlParser.GetVideoId(url);

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _sessionCoordinator.StateChanged -= HandleSessionStateChanged;
        _sessionCoordinator.SocketConnectionChanged -= HandleSocketConnectionChanged;
        _sessionCoordinator.PlaybackStateChanged -= HandlePlaybackStateChanged;
        _sessionCoordinator.PlaylistCreated -= HandlePlaylistCreated;
        _sessionCoordinator.PlaylistDeleted -= HandlePlaylistDeleted;
        _sessionCoordinator.ErrorOccurred -= HandleErrorOccurred;
        GC.SuppressFinalize(this);
    }
}
