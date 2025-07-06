// This file is part of YouTubeMusicStreamer.
// Copyright (C) 2025 Dominic Ris
// 
// YouTubeMusicStreamer is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as published
// by the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version (the "AGPLv3").
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
using System.Web;
using Microsoft.Extensions.Logging;
using XeroxDev.YTMDesktop.Companion;
using XeroxDev.YTMDesktop.Companion.Clients;
using XeroxDev.YTMDesktop.Companion.Enums;
using XeroxDev.YTMDesktop.Companion.Exceptions;
using XeroxDev.YTMDesktop.Companion.Models.Output;
using XeroxDev.YTMDesktop.Companion.Settings;
using YouTubeMusicStreamer.Enums;
using YouTubeMusicStreamer.Services.App;
using YouTubeMusicStreamer.Services.WebSocket;

namespace YouTubeMusicStreamer.Services.YouTube;

public sealed partial class YouTubeService : INotifyPropertyChanged
{
    #region Fields and Properties

    private readonly ILogger<YouTubeService> _logger;
    private readonly SettingsService _settings;
    private readonly WebSocketService _webSocketService;
    private readonly SongQueueService _songQueueService;
    private readonly ConnectorSettings _connectorSettings;
    public CompanionConnector? CompanionConnector { get; private set; }

    public RestClient? RestClient => CompanionConnector?.RestClient;
    public SocketClient? SocketClient => CompanionConnector?.SocketClient;

    private ConnectorState _state = ConnectorState.Loading;

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

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        OnPropertyChanged(propertyName);
    }

    #endregion

    #region Events

    public event EventHandler<Exception> OnError = delegate { };

    /// <summary>
    ///     The event that is raised when the socket connection is changed.
    /// </summary>
    public event EventHandler<ESocketState> OnConnectionChange = delegate { };

    /// <summary>
    ///     The event that is raised when the YTMDesktop State has changed.
    /// </summary>
    public event EventHandler<StateOutput> OnStateChange = delegate { };

    /// <summary>
    ///     The event that is raised when a playlist was created
    /// </summary>
    public event EventHandler<PlaylistOutput> OnPlaylistCreated = delegate { };

    /// <summary>
    ///     The event that is raised when a playlist was deleted
    /// </summary>
    public event EventHandler<string> OnPlaylistDeleted = delegate { };

    #endregion

    public YouTubeService(ILogger<YouTubeService> logger, SettingsService settings, WebSocketService webSocketService, SongQueueService songQueueService, VersionService versionService)
    {
        _logger = logger;
        _settings = settings;
        _webSocketService = webSocketService;
        _songQueueService = songQueueService;

        _connectorSettings = new ConnectorSettings(
            GetValidatedHost(),
            GetValidatedPort(),
            AppInfo.Current.Name.ToLowerInvariant().Replace(" ", "-"),
            AppInfo.Current.Name,
            versionService.GetAppVersion().ToNormalizedString(),
            settings.GetSensitiveSettings().YtmDesktopToken
        );
        _ = InitializeAsync();
    }

    private async Task InitializeAsync(bool forceNewToken = false)
    {
        State = ConnectorState.Loading;

        if (CompanionConnector is null)
        {
            CompanionConnector = new CompanionConnector(_connectorSettings);
            SocketClient!.OnError += (sender, e) => OnError(sender, e);
            SocketClient.OnConnectionChange += (sender, e) =>
            {
                OnConnectionChange(sender, e);
                ConnectionState = e;
            };
            SocketClient.OnStateChange += (sender, e) =>
            {
                OnStateChange(sender, e);

                _ = Task.Run(() =>
                {
                    _ = _songQueueService.YouTubeStateChanged(this, e);

                    if (!_webSocketService.IsRunning) return;
                    _ = _webSocketService.BroadcastMessageAsync(SocketEvents.TrackInfo, e);
                });
            };
            SocketClient.OnPlaylistCreated += (sender, e) => OnPlaylistCreated(sender, e);
            SocketClient.OnPlaylistDeleted += (sender, e) => OnPlaylistDeleted(sender, e);
        }

        MetadataOutput? metadata = null;

        try
        {
            metadata = await RestClient!.GetMetadata();
        }
        catch (ApiException e)
        {
            SendError($"{e.Error.Error}, {e.Error.Message}", e);
        }
        catch (Exception e)
        {
            SendError(e.Message, e);
        }

        if (metadata is null)
        {
            if (!string.IsNullOrEmpty(_connectorSettings.Token))
            {
                SendError("YTMDesktop is not started or can't be found. Due to last successful connection, Auto-Retry every 5 seconds is enabled");
                await Task.Delay(5000);
                _ = InitializeAsync(forceNewToken);
            }
            else
            {
                SendError("YTMDesktop is not started or can't be found");
            }

            return;
        }

        if (forceNewToken)
        {
            try
            {
                // If not, try to request one and show it to the user
                var code = await RestClient!.GetAuthCode();
                if (code is null)
                {
                    SendError("Couldn't get code from YTMDesktop.");
                    return;
                }

                State = ConnectorState.LoggingIn;
                Code = code;

                var token = await RestClient.GetAuthToken(code);
                if (string.IsNullOrWhiteSpace(token))
                {
                    SendError("Couldn't get token from YTMDesktop.");

                    return;
                }

                // Save token to file
                await _settings.SaveSensitiveSettingAsync(s => s.YtmDesktopToken = token);

                CompanionConnector.SetAuthToken(token);

                State = ConnectorState.LoggedIn;
                Error = string.Empty;
                Code = string.Empty;
                _logger.LogInformation("Token successfully saved");
            }
            catch (ApiException e)
            {
                SendError($"{e.Error.Error}, {e.Error.Message}", e);
            }
            catch (Exception e)
            {
                SendError(e.Message, e);
            }
        }

        if (string.IsNullOrEmpty(_connectorSettings.Token))
        {
            SendError("Not authorized to connect to YTMDesktop");
            return;
        }

        await SocketClient!.Connect();

        State = ConnectorState.LoggedIn;
        Error = string.Empty;
        Code = string.Empty;
        _logger.LogInformation("Connected to YTMDesktop Companion");
    }

    public async Task SetAddressAsync(string host, int port)
    {
        _connectorSettings.Host = GetValidatedHost(host);
        _connectorSettings.Port = GetValidatedPort(port);
        _connectorSettings.Token = null;
        CompanionConnector?.SetAuthToken(null);

        await _settings.SaveAppSettingAsync(s =>
        {
            s.YouTubeHost = _connectorSettings.Host;
            s.YouTubePort = _connectorSettings.Port;
        });
        await _settings.SaveSensitiveSettingAsync(s => s.YtmDesktopToken = _connectorSettings.Token);

        await InitializeAsync(true);
    }

    public string GetValidatedHost(string? host = null)
    {
        host ??= _settings.GetAppSettings().YouTubeHost ?? "127.0.0.1";
        if (host.Equals("localhost", StringComparison.InvariantCultureIgnoreCase)) host = "127.0.0.1";

        return host;
    }

    public int GetValidatedPort(int? port = null)
    {
        port ??= _settings.GetAppSettings().YouTubePort ?? 9863;
        if (port is <= 0 or > 65535) port = 9863;

        return port.Value;
    }

    private void SendError(string message, Exception? e = null)
    {
        State = ConnectorState.Error;
        Error = message;
        _logger.LogError("{Message}", message);
        OnError(this, new Exception(message, e));
    }

    public static string? GetVideoId(string url)
    {
        Uri uri;
        try
        {
            uri = new Uri(url);
        }
        catch (Exception)
        {
            return null;
        }

        // if it's a youtu.be link, use the path (without the / and without any query string)

        if (uri.Host.Contains("youtu.be"))
        {
            return uri.PathAndQuery.TrimStart('/').Split('?')[0];
        }

        var query = HttpUtility.ParseQueryString(uri.Query);
        var videoId = query["v"];
        return string.IsNullOrWhiteSpace(videoId) ? null : videoId;
    }
}