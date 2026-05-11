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

using Microsoft.Extensions.Logging;
using YouTubeMusicStreamer.Services.App;
using YouTubeMusicStreamer.Services.Commands;
using YouTubeMusicStreamer.Services.Twitch;
using YouTubeMusicStreamer.Services.Twitch.Interfaces;
using YouTubeMusicStreamer.Services.WebSocket;
using YouTubeMusicStreamer.Services.YouTube;

namespace YouTubeMusicStreamer.Services.Startup;

public sealed class StartupRuntimeServices : IStartupRuntimeServices
{
    private readonly IServiceProvider _serviceProvider;
    private readonly SettingsService _settingsService;
    private readonly CommandService _commandService;
    private readonly VersionService _versionService;
    private readonly YouTubeService _youTubeService;
    private readonly TwitchService _twitchService;
    private readonly WebSocketService _webSocketService;

    public StartupRuntimeServices(
        IServiceProvider serviceProvider,
        SettingsService settingsService,
        CommandService commandService,
        VersionService versionService,
        YouTubeService youTubeService,
        TwitchService twitchService,
        WebSocketService webSocketService)
    {
        _serviceProvider = serviceProvider;
        _settingsService = settingsService;
        _commandService = commandService;
        _versionService = versionService;
        _youTubeService = youTubeService;
        _twitchService = twitchService;
        _webSocketService = webSocketService;
    }

    public Exception? LastVersionCheckError => _versionService.LastCheckError;
    public bool ShouldAutoStartWidgetServer => _settingsService.GetYouTubeSettings().AutoStartServer;

    public Task InitializeSettingsAsync() => _settingsService.InitializeAsync();
    public Task PreloadSensitiveSettingsAsync() => _settingsService.PreloadSensitiveSettingsAsync();
    public Task InitializeCommandsAsync() => _commandService.InitializeAsync();
    public Task InitializeVersionAsync() => _versionService.InitializeIfNeededAsync();
    public Task InitializeYouTubeAsync() => _youTubeService.InitializeIfNeededAsync();
    public Task InitializeTwitchAsync() => _twitchService.InitializeIfNeededAsync();
    public Task StartWidgetServerAsync() => _webSocketService.StartAsync();
    public void StopWidgetServer() => _webSocketService.Stop();

    public IReadOnlyList<StartupCommandDescriptor> ListCommands() =>
        _commandService.ListCommands()
            .Select(command => new StartupCommandDescriptor(command.Trigger, command.Description, command.IsEnabled))
            .ToList();

    public void ValidateDevelopmentConfiguration()
    {
#if DEBUG
        _ = _serviceProvider.GetRequiredService<SettingsService>();
        _ = _serviceProvider.GetRequiredService<CommandService>();
        _ = _serviceProvider.GetRequiredService<VersionService>();
        _ = _serviceProvider.GetRequiredService<YouTubeService>();
        _ = _serviceProvider.GetRequiredService<TwitchService>();
        _ = _serviceProvider.GetRequiredService<WebSocketService>();
        _ = _serviceProvider.GetRequiredService<WebSocketClientService>();
        _ = _serviceProvider.GetRequiredService<AudioService>();
        _ = _serviceProvider.GetRequiredService<SongQueueService>();
        _ = _serviceProvider.GetRequiredService<ITwitchTokenService>();
        _ = _serviceProvider.GetRequiredService<ITwitchUserService>();
        _ = _serviceProvider.GetRequiredService<ITwitchChatService>();
        _ = _serviceProvider.GetRequiredService<ITwitchEventSubService>();
        _ = _serviceProvider.GetRequiredService<ITwitchAuthService>();
#endif
    }
}
