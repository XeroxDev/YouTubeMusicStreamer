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

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using TwitchLib.EventSub.Websockets.Extensions;
using YouTubeMusicStreamer.Components.Pages.About;
using YouTubeMusicStreamer.Components.Pages.YTMDesktop;
using YouTubeMusicStreamer.Services.App;
using YouTubeMusicStreamer.Services.App.Diagnostics;
using YouTubeMusicStreamer.Services.App.Persistence;
using YouTubeMusicStreamer.Services.Commands;
using YouTubeMusicStreamer.Services.Commands.ArgumentParser;
using YouTubeMusicStreamer.Services.Commands.Binding;
using YouTubeMusicStreamer.Services.Commands.Cooldowns;
using YouTubeMusicStreamer.Services.Commands.Formatting;
using YouTubeMusicStreamer.Services.Commands.Placeholders;
using YouTubeMusicStreamer.Services.Commands.PrerequisiteChecking;
using YouTubeMusicStreamer.Services.Commands.Workflows;
using YouTubeMusicStreamer.Services.Twitch;
using YouTubeMusicStreamer.Services.Twitch.Implementations;
using YouTubeMusicStreamer.Services.Twitch.Interfaces;
using YouTubeMusicStreamer.Services.WebSocket;
using YouTubeMusicStreamer.Services.YouTube;
using YouTubeMusicStreamer.Services.Startup;
using YouTubeMusicStreamer.Utils;

namespace YouTubeMusicStreamer;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        var appPathProvider = new AppPathProvider();
        builder.UseMauiApp<App>();

        LoggingConfig.ConfigureFileLogging(builder.Logging);

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddHttpClient();
        builder.Services.AddTwitchLibEventSubWebsockets();
        builder.Services.AddSingleton<IAsyncDelay, SystemAsyncDelay>();
        builder.Services.AddSingleton<IAboutPlatformAccess, MauiAboutPlatformAccess>();
        builder.Services.AddSingleton<IAboutAssetService, AboutAssetService>();
        builder.Services.AddSingleton<IYtmDesktopClientExportPlatform, MauiYtmDesktopClientExportPlatform>();
        builder.Services.AddSingleton<IYtmDesktopClientExportService, YtmDesktopClientExportService>();
        builder.Services.AddScoped<IAppToastService, AppToastService>();
        builder.Services.AddScoped<AppNotificationBridge>();
        builder.Services.AddTransient<LogViewerFacade>();
        builder.Services.AddSingleton<ILogViewerEnvironment, LogViewerEnvironment>();
        builder.Services.AddTransient<QueuePageFacade>();
        builder.Services.AddTransient<TwitchSettingsPageFacade>();
        builder.Services.AddTransient<TwitchRewardsFacade>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Services.AddSassCompiler();
#endif

        builder.Services
            .AddDbContextFactory<AppDbContext>(options =>
            {
                options.UseSqlite($"Data Source={appPathProvider.DatabaseFilePath}");
                options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
                options.ConfigureWarnings(warnings =>
                {
                    warnings.Ignore(RelationalEventId.CommandExecuted);
                    warnings.Ignore(RelationalEventId.CommandExecuting);
                    warnings.Ignore(RelationalEventId.AcquiringMigrationLock);
                    warnings.Ignore(RelationalEventId.MigrationsNotApplied);
                });
            })
            .AddSingleton<IAppDiagnosticsService, AppDiagnosticsService>()
            .AddSingleton<IAppPathProvider>(appPathProvider)
            .AddSingleton<IAppLaunchState>(_ => AppLaunchStateCapture.CreateState())
            .AddSingleton<IAppIdentitySource, AssemblyAppIdentitySource>()
            .AddSingleton<IAppVersionSource, MauiAppVersionSource>()
            .AddSingleton<IAppUpdateManagerFactory, VelopackUpdateManagerFactory>()
            .AddSingleton<IStartupPlatform, StartupPlatform>()
            .AddSingleton<IInstanceCommandServerFactory, InstanceCommandServerFactory>()
            .AddSingleton<IStartupStateStore, FileStartupStateStore>()
            .AddSingleton<SettingsService>()
            .AddSingleton<CommandRegistry>()
            .AddSingleton<ICommandCatalog>(sp => sp.GetRequiredService<CommandRegistry>())
            .AddSingleton<CommandService>()
            .AddSingleton<IArgumentParser, ArgumentParser>()
            .AddSingleton<IPrerequisiteChecker, PrerequisiteChecker>()
            .AddSingleton<ICooldownManager, CooldownManager>()
            .AddSingleton<IArgumentBinder, ReflectionArgumentBinder>()
            .AddSingleton<IResponseFormatter, ResponseFormatter>()
            .AddSingleton<IPlaceholderProvider, ReflectionPlaceholderProvider>()
            .AddSingleton<IYouTubeCommandWorkflow, YouTubeCommandWorkflow>()
            .AddSingleton<IYouTubeMetadataResolver, YouTubeMetadataResolver>()
            .AddSingleton<ITwitchApiFactory, TwitchApiFactory>()
            .AddSingleton<ITwitchTokenService, TwitchTokenService>()
            .AddSingleton<ITwitchUserService, TwitchUserService>()
            .AddSingleton<ITwitchRewardService, TwitchRewardService>()
            .AddSingleton<ITwitchChatClientFactory, TwitchChatClientFactory>()
            .AddSingleton<ITwitchChatService, TwitchChatService>()
            .AddSingleton<ITwitchEventSubTransport, TwitchEventSubTransport>()
            .AddSingleton<ITwitchEventSubSubscriptionClientFactory, TwitchEventSubSubscriptionClientFactory>()
            .AddSingleton<ITwitchEventSubService, TwitchEventSubService>()
            .AddSingleton<ITwitchAuthBrowserLauncher, MauiTwitchAuthBrowserLauncher>()
            .AddSingleton<ITwitchAuthCallbackListenerFactory, HttpListenerTwitchAuthCallbackListenerFactory>()
            .AddSingleton<ITwitchAuthService, TwitchBrowserAuthService>()
            .AddSingleton<ITwitchSessionCoordinator, TwitchSessionCoordinator>()
            .AddSingleton<TwitchService>()
            .AddSingleton<ITwitchStatusSource>(sp => sp.GetRequiredService<TwitchService>())
            .AddSingleton<IYtmCompanionConnectorFactory, YtmCompanionConnectorFactory>()
            .AddSingleton<IYtmCompanionSessionCoordinator, YtmCompanionSessionCoordinator>()
            .AddSingleton<YouTubeService>()
            .AddSingleton<IYouTubeStatusSource>(sp => sp.GetRequiredService<YouTubeService>())
            .AddSingleton<IYtmPlaybackController, YtmPlaybackController>()
            .AddSingleton<YtmDesktopFacade>()
            .AddSingleton<IAudioDeviceProvider, NAudioDeviceProvider>()
            .AddSingleton<IAudioCaptureSessionFactory, WasapiLoopbackCaptureSessionFactory>()
            .AddSingleton<AudioService>()
            .AddSingleton<IAudioCaptureService>(sp => sp.GetRequiredService<AudioService>())
            .AddSingleton<IWidgetServerHost, HttpListenerWidgetServerHost>()
            .AddSingleton<WebSocketService>()
            .AddSingleton<IWidgetServerStatusSource>(sp => sp.GetRequiredService<WebSocketService>())
            .AddSingleton<IWidgetServerController>(sp => sp.GetRequiredService<WebSocketService>())
            .AddSingleton<WebSocketClientService>()
            .AddSingleton<SongQueueService>()
            .AddSingleton<VersionService>()
            .AddSingleton<IVersionStatusSource>(sp => sp.GetRequiredService<VersionService>())
            .AddSingleton<IStartupRuntimeServices, StartupRuntimeServices>()
            .AddSingleton<IStartupCoordinator, StartupCoordinator>();

#if DEBUG
        ValidateServiceRegistrations(builder.Services);
#endif
        return builder.Build();
    }

#if DEBUG
    private static void ValidateServiceRegistrations(IServiceCollection services)
    {
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
    }
#endif
}
