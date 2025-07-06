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

using Blazored.Toast;
using TwitchLib.Api;
using TwitchLib.EventSub.Websockets.Extensions;
using YouTubeMusicStreamer.Services.App;
using YouTubeMusicStreamer.Services.Commands;
using YouTubeMusicStreamer.Services.Commands.ArgumentParser;
using YouTubeMusicStreamer.Services.Commands.Binding;
using YouTubeMusicStreamer.Services.Commands.Cooldowns;
using YouTubeMusicStreamer.Services.Commands.Formatting;
using YouTubeMusicStreamer.Services.Commands.Placeholders;
using YouTubeMusicStreamer.Services.Commands.PrerequisiteChecking;
using YouTubeMusicStreamer.Services.Twitch;
using YouTubeMusicStreamer.Services.Twitch.Implementations;
using YouTubeMusicStreamer.Services.Twitch.Interfaces;
using YouTubeMusicStreamer.Services.WebSocket;
using YouTubeMusicStreamer.Services.YouTube;
using YouTubeMusicStreamer.Utils;

namespace YouTubeMusicStreamer;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        LoggingConfig.ConfigureFileLogging(builder.Logging);

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddBlazoredToast();
        builder.Services.AddTwitchLibEventSubWebsockets();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Services.AddSassCompiler();
#endif

        builder.Services
            .AddSingleton<SettingsService>()
            .AddSingleton<CommandService>()
            .AddSingleton<CommandBag>()
            .AddSingleton<IArgumentParser, ArgumentParser>()
            .AddSingleton<IPrerequisiteChecker, PrerequisiteChecker>()
            .AddSingleton<ICooldownManager, CooldownManager>()
            .AddSingleton<IArgumentBinder, ReflectionArgumentBinder>()
            .AddSingleton<IResponseFormatter, ResponseFormatter>()
            .AddSingleton<IPlaceholderProvider, ReflectionPlaceholderProvider>()
            .AddSingleton<TwitchAPI>(_ => new TwitchAPI { Settings = { ClientId = GeneratedBuildInfo.TwitchClientId } })
            .AddSingleton<ITwitchTokenService, TwitchTokenService>()
            .AddSingleton<ITwitchUserService, TwitchUserService>()
            .AddSingleton<ITwitchChatService, TwitchChatService>()
            .AddSingleton<ITwitchEventSubService, TwitchEventSubService>()
            .AddSingleton<TwitchService>()
            .AddSingleton<YouTubeService>()
            .AddSingleton<WebSocketService>()
            .AddSingleton<WebSocketClientService>()
            .AddSingleton<AudioService>()
            .AddSingleton<SongQueueService>()
            .AddSingleton<VersionService>();

        return builder.Build();
    }
}