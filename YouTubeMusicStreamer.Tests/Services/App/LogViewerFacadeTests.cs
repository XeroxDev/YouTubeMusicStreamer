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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TwitchLib.EventSub.Core.Models.Chat;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;
using TwitchLib.EventSub.Websockets.Core.EventArgs;
using TwitchLib.Api.Helix.Models.ChannelPoints;
using XeroxDev.YTMDesktop.Companion.Enums;
using XeroxDev.YTMDesktop.Companion.Models.Output;
using YouTubeMusicStreamer.Services.App;
using YouTubeMusicStreamer.Services.App.Diagnostics;
using YouTubeMusicStreamer.Services.App.Persistence;
using YouTubeMusicStreamer.Services.Startup;
using YouTubeMusicStreamer.Services.Twitch;
using YouTubeMusicStreamer.Services.WebSocket;
using YouTubeMusicStreamer.Services.YouTube;
using YouTubeMusicStreamer.Tests.TestSupport;

namespace YouTubeMusicStreamer.Tests.Services.App;

public sealed class LogViewerFacadeTests
{
    [Fact]
    public async Task Initialize_LoadsConfigurationLogsDiagnosticsAndHealthSummaries()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        await harness.Settings.InitializeAsync();
        var settings = harness.Settings;
        await settings.SaveAppConfigurationAsync(new AppConfigurationSnapshot(LogLevel.Information));

        var diagnostics = new RecordingDiagnosticsService();
        var retainedDiagnostic = diagnostics.Record(
            AppDiagnosticSubsystem.Commands,
            AppDiagnosticSeverity.Warning,
            AppDiagnosticCategory.Configuration,
            "Command warning",
            "Detailed context",
            new InvalidOperationException("boom"),
            AppDiagnosticVisibility.Toast);

        var startup = new FakeStartupCoordinator
        {
            CurrentHealth = new InstanceHealthState(
                42,
                "launch-token",
                DateTime.UtcNow.AddMinutes(-2),
                DateTime.UtcNow,
                InstanceRuntimeState.Unhealthy,
                StartupPhase.Failed,
                "1.0.0"),
            CurrentIssues =
            [
                new StartupIssue(StartupIssueSeverity.Fatal, "Startup", "Startup failed", DateTime.UtcNow, "detail")
            ]
        };

        var twitch = new FakeTwitchStatusSource
        {
            SessionStatus = TwitchSessionStatus.Degraded,
            Username = "Broadcaster",
            BotUsername = "BotAccount"
        };
        var youTube = new FakeYouTubeStatusSource
        {
            SessionState = new YtmCompanionSessionState(
                new YtmEndpointConfiguration("127.0.0.1", 9863),
                YtmEndpointStatus.ValidCompanion,
                YtmAuthorizationStatus.Authorized,
                YtmConnectionStatus.Degraded,
                ESocketState.Connected,
                "Playback degraded",
                null,
                true,
                null),
            Error = "Playback degraded"
        };
        var widget = new FakeWidgetServerStatusSource
        {
            State = new WidgetServerState(
                new WidgetServerConfiguration(8080, true, "device-1"),
                WidgetServerStatus.Degraded,
                3,
                null,
                new AudioSubsystemState(AudioCaptureStatus.Capturing, "device-1", null),
                null)
        };
        var version = new FakeVersionStatusSource
        {
            Status = VersionService.UpdateStatus.Available,
            StatusText = "New update available—v1.2.3"
        };
        var environment = new FakeLogViewerEnvironment
        {
            LogFolderPath = @"C:\Logs",
            CurrentLogFileName = "current.log",
            LogLinesByPath =
            {
                [Path.Combine(@"C:\Logs", "current.log")] = Enumerable.Range(1, 120).Select(i => $"line-{i}").ToList()
            }
        };

        using var facade = CreateFacade(settings, diagnostics, startup, twitch, youTube, widget, version, environment);

        facade.Initialize();

        Assert.Equal(LogLevel.Information, facade.Configuration.LogLevel);
        Assert.Equal(100, facade.LogLines.Count);
        Assert.Equal("line-21", facade.LogLines[0]);
        Assert.Equal("line-120", facade.LogLines[^1]);
        var diagnostic = Assert.Single(facade.RecentDiagnostics);
        Assert.Equal(retainedDiagnostic.Id, diagnostic.Id);

        Assert.Collection(
            facade.HealthSummaries,
            startupSummary =>
            {
                Assert.Equal("Startup", startupSummary.Name);
                Assert.Equal("Unhealthy", startupSummary.Status);
                Assert.Equal("Startup failed", startupSummary.Detail);
                Assert.Equal("danger", startupSummary.Tone);
            },
            twitchSummary =>
            {
                Assert.Equal("Twitch", twitchSummary.Name);
                Assert.Equal("Degraded", twitchSummary.Status);
                Assert.Equal("Broadcaster: Broadcaster | Bot: BotAccount", twitchSummary.Detail);
                Assert.Equal("warning", twitchSummary.Tone);
            },
            youTubeSummary =>
            {
                Assert.Equal("YTMDesktop", youTubeSummary.Name);
                Assert.Equal("Degraded", youTubeSummary.Status);
                Assert.Equal("Playback degraded", youTubeSummary.Detail);
                Assert.Equal("warning", youTubeSummary.Tone);
            },
            widgetSummary =>
            {
                Assert.Equal("Widget Server", widgetSummary.Name);
                Assert.Equal("Degraded", widgetSummary.Status);
                Assert.Equal("Clients: 3", widgetSummary.Detail);
                Assert.Equal("warning", widgetSummary.Tone);
            },
            audioSummary =>
            {
                Assert.Equal("Audio Capture", audioSummary.Name);
                Assert.Equal("Capturing", audioSummary.Status);
                Assert.Equal("device-1", audioSummary.Detail);
                Assert.Equal("success", audioSummary.Tone);
            },
            updateSummary =>
            {
                Assert.Equal("Updates", updateSummary.Name);
                Assert.Equal("Available", updateSummary.Status);
                Assert.Equal("New update available—v1.2.3", updateSummary.Detail);
                Assert.Equal("warning", updateSummary.Tone);
            });

        Assert.Equal(@"C:\Logs", environment.WatchedDirectoryPath);
        Assert.Equal("current.log", environment.WatchedFileName);
    }

    [Fact]
    public async Task SaveConfigurationAsync_UpdatesConfiguration_WhenSettingsRaiseChange()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        await harness.Settings.InitializeAsync();
        var settings = harness.Settings;
        var facade = CreateFacade(
            settings,
            new RecordingDiagnosticsService(),
            new FakeStartupCoordinator(),
            new FakeTwitchStatusSource(),
            new FakeYouTubeStatusSource(),
            new FakeWidgetServerStatusSource(),
            new FakeVersionStatusSource(),
            new FakeLogViewerEnvironment());

        var stateChangedCount = 0;
        facade.StateChanged += (_, _) => stateChangedCount++;
        facade.Initialize();

        await facade.SaveConfigurationAsync(new AppConfigurationSnapshot(LogLevel.Error));

        Assert.Equal(LogLevel.Error, facade.Configuration.LogLevel);
        Assert.True(stateChangedCount >= 1);
    }

    [Fact]
    public async Task CopyDiagnosticsSummaryAsync_UsesEnvironmentClipboard()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        await harness.Settings.InitializeAsync();
        var settings = harness.Settings;
        await settings.SaveAppConfigurationAsync(new AppConfigurationSnapshot(LogLevel.Debug));

        var diagnostics = new RecordingDiagnosticsService();
        diagnostics.Record(
            AppDiagnosticSubsystem.Logging,
            AppDiagnosticSeverity.Error,
            AppDiagnosticCategory.InternalFault,
            "Log reader failed",
            "bad file",
            new InvalidOperationException("stack boom"),
            AppDiagnosticVisibility.DiagnosticsOnly);

        var environment = new FakeLogViewerEnvironment();
        using var facade = CreateFacade(
            settings,
            diagnostics,
            new FakeStartupCoordinator(),
            new FakeTwitchStatusSource(),
            new FakeYouTubeStatusSource(),
            new FakeWidgetServerStatusSource(),
            new FakeVersionStatusSource(),
            environment);

        facade.Initialize();
        await facade.CopyDiagnosticsSummaryAsync();

        Assert.Contains("Generated:", environment.LastClipboardText);
        Assert.Contains("Log level: Debug", environment.LastClipboardText);
        Assert.Contains("Log reader failed (bad file)", environment.LastClipboardText);
        Assert.Contains("Exception:", environment.LastClipboardText);
        Assert.Contains("stack boom", environment.LastClipboardText);
    }

    [Fact]
    public async Task ClearDiagnosticsAsync_RefreshesStateAndRaisesStateChanged()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        await harness.Settings.InitializeAsync();
        var settings = harness.Settings;
        var diagnostics = new RecordingDiagnosticsService();
        diagnostics.Record(
            AppDiagnosticSubsystem.Commands,
            AppDiagnosticSeverity.Warning,
            AppDiagnosticCategory.Configuration,
            "warning");

        using var facade = CreateFacade(
            settings,
            diagnostics,
            new FakeStartupCoordinator(),
            new FakeTwitchStatusSource(),
            new FakeYouTubeStatusSource(),
            new FakeWidgetServerStatusSource(),
            new FakeVersionStatusSource(),
            new FakeLogViewerEnvironment());

        var stateChangedCount = 0;
        facade.StateChanged += (_, _) => stateChangedCount++;
        facade.Initialize();

        await facade.ClearDiagnosticsAsync();

        Assert.Empty(facade.RecentDiagnostics);
        Assert.True(stateChangedCount >= 1);
    }

    [Fact]
    public async Task FileChangeAndServiceEvents_RefreshState_AndDisposeStopsFurtherUpdates()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        await harness.Settings.InitializeAsync();
        var settings = harness.Settings;
        var diagnostics = new RecordingDiagnosticsService();
        var startup = new FakeStartupCoordinator();
        var twitch = new FakeTwitchStatusSource();
        var youTube = new FakeYouTubeStatusSource();
        var widget = new FakeWidgetServerStatusSource();
        var version = new FakeVersionStatusSource();
        var environment = new FakeLogViewerEnvironment
        {
            LogFolderPath = @"C:\Logs",
            CurrentLogFileName = "current.log",
            LogLinesByPath =
            {
                [Path.Combine(@"C:\Logs", "current.log")] = ["first"]
            }
        };

        using var facade = CreateFacade(settings, diagnostics, startup, twitch, youTube, widget, version, environment);
        var stateChangedCount = 0;
        facade.StateChanged += (_, _) => stateChangedCount++;
        facade.Initialize();

        environment.LogLinesByPath[Path.Combine(@"C:\Logs", "current.log")] = ["second"];
        environment.RaiseFileChanged(Path.Combine(@"C:\Logs", "current.log"));
        diagnostics.Record(AppDiagnosticSubsystem.Commands, AppDiagnosticSeverity.Info, AppDiagnosticCategory.Configuration, "new");
        startup.RaiseHealthChanged(startup.CurrentHealth with { State = InstanceRuntimeState.Ready });
        twitch.RaisePropertyChanged(nameof(ITwitchStatusSource.SessionStatus));
        youTube.RaisePropertyChanged(nameof(IYouTubeStatusSource.Error));
        widget.RaiseStateChanged(widget.State with { Status = WidgetServerStatus.Running });
        version.RaiseChanged();

        Assert.Equal("second", Assert.Single(facade.LogLines));
        Assert.True(stateChangedCount >= 6);

        facade.Dispose();

        environment.LogLinesByPath[Path.Combine(@"C:\Logs", "current.log")] = ["third"];
        environment.RaiseFileChanged(Path.Combine(@"C:\Logs", "current.log"));
        diagnostics.Record(AppDiagnosticSubsystem.Commands, AppDiagnosticSeverity.Info, AppDiagnosticCategory.Configuration, "after-dispose");

        Assert.Equal("second", Assert.Single(facade.LogLines));
    }

    [Fact]
    public async Task Initialize_UsesEmptyLogLines_WhenEnvironmentReadThrows()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        await harness.Settings.InitializeAsync();
        var settings = harness.Settings;
        var environment = new FakeLogViewerEnvironment
        {
            ReadException = new InvalidOperationException("log failed")
        };

        using var facade = CreateFacade(
            settings,
            new RecordingDiagnosticsService(),
            new FakeStartupCoordinator(),
            new FakeTwitchStatusSource(),
            new FakeYouTubeStatusSource(),
            new FakeWidgetServerStatusSource(),
            new FakeVersionStatusSource(),
            environment);

        facade.Initialize();

        Assert.Empty(facade.LogLines);
    }

    [Fact]
    public async Task FileChange_IgnoresUnrelatedFiles_InWatchedDirectory()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        await harness.Settings.InitializeAsync();
        var settings = harness.Settings;
        var environment = new FakeLogViewerEnvironment
        {
            LogFolderPath = @"C:\Logs",
            CurrentLogFileName = "current.log",
            LogLinesByPath =
            {
                [Path.Combine(@"C:\Logs", "current.log")] = ["current"],
                [Path.Combine(@"C:\Logs", "other.log")] = ["other"]
            }
        };

        using var facade = CreateFacade(
            settings,
            new RecordingDiagnosticsService(),
            new FakeStartupCoordinator(),
            new FakeTwitchStatusSource(),
            new FakeYouTubeStatusSource(),
            new FakeWidgetServerStatusSource(),
            new FakeVersionStatusSource(),
            environment);

        facade.Initialize();
        environment.RaiseFileChanged(Path.Combine(@"C:\Logs", "other.log"));

        Assert.Equal("current", Assert.Single(facade.LogLines));
    }

    [Fact]
    public async Task OpenLogFolder_UsesEnvironmentFolderPath()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        await harness.Settings.InitializeAsync();
        var settings = harness.Settings;
        var environment = new FakeLogViewerEnvironment
        {
            LogFolderPath = @"C:\Logs"
        };

        using var facade = CreateFacade(
            settings,
            new RecordingDiagnosticsService(),
            new FakeStartupCoordinator(),
            new FakeTwitchStatusSource(),
            new FakeYouTubeStatusSource(),
            new FakeWidgetServerStatusSource(),
            new FakeVersionStatusSource(),
            environment);

        facade.OpenLogFolder();

        Assert.Equal(@"C:\Logs", environment.LastOpenedFolderPath);
    }

    private static LogViewerFacade CreateFacade(
        SettingsService settings,
        RecordingDiagnosticsService diagnostics,
        FakeStartupCoordinator startup,
        FakeTwitchStatusSource twitch,
        FakeYouTubeStatusSource youTube,
        FakeWidgetServerStatusSource widget,
        FakeVersionStatusSource version,
        FakeLogViewerEnvironment environment) =>
        new(
            settings,
            diagnostics,
            startup,
            twitch,
            youTube,
            widget,
            version,
            environment,
            NullLogger<LogViewerFacade>.Instance);

    private sealed class FakeStartupCoordinator : IStartupCoordinator
    {
        public InstanceHealthState CurrentHealth { get; set; } = new(
            1,
            "token",
            DateTime.UtcNow,
            DateTime.UtcNow,
            InstanceRuntimeState.Starting,
            StartupPhase.NotStarted,
            "1.0.0");

        public IReadOnlyList<StartupIssue> CurrentIssues { get; set; } = [];
        public event EventHandler<InstanceHealthState>? HealthChanged;

        public void InitializePrimaryInstanceInfrastructure() { }
        public void OnWindowCreated(Window? window) { }
        public void OnWindowDestroying() { }

        public void RaiseHealthChanged(InstanceHealthState health)
        {
            CurrentHealth = health;
            HealthChanged?.Invoke(this, health);
        }
    }

    private sealed class FakeTwitchStatusSource : ITwitchStatusSource
    {
        public TwitchSessionStatus SessionStatus { get; set; } = TwitchSessionStatus.NotConfigured;
        public string? Username { get; set; }
        public string? BroadcasterAccountId { get; set; }
        public string? BotUsername { get; set; }
        public event PropertyChangedEventHandler? PropertyChanged;

        public void RaisePropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private sealed class FakeYouTubeStatusSource : IYouTubeStatusSource
    {
        public YtmCompanionSessionState SessionState { get; set; } = new(
            new YtmEndpointConfiguration(null, null),
            YtmEndpointStatus.Unknown,
            YtmAuthorizationStatus.NotConfigured,
            YtmConnectionStatus.Disconnected,
            ESocketState.Disconnected,
            null,
            null,
            false,
            null);

        public string Error { get; set; } = string.Empty;
        public event PropertyChangedEventHandler? PropertyChanged;

        public void RaisePropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private sealed class FakeWidgetServerStatusSource : IWidgetServerStatusSource
    {
        public WidgetServerState State { get; set; } = new(
            new WidgetServerConfiguration(8080, false, string.Empty),
            WidgetServerStatus.Stopped,
            0,
            null,
            new AudioSubsystemState(AudioCaptureStatus.Disabled, null, null),
            null);

        public event EventHandler<WidgetServerState> StateChanged = delegate { };

        public void RaiseStateChanged(WidgetServerState state)
        {
            State = state;
            StateChanged(this, state);
        }
    }

    private sealed class FakeVersionStatusSource : IVersionStatusSource
    {
        public VersionService.UpdateStatus Status { get; set; } = VersionService.UpdateStatus.Pending;
        public string StatusText { get; set; } = "Checking for updates…";
        public event Action? OnChange;

        public void RaiseChanged() => OnChange?.Invoke();
    }

    private sealed class FakeLogViewerEnvironment : ILogViewerEnvironment
    {
        private Action<string>? _onChanged;

        public string LogFolderPath { get; set; } = @"C:\Logs";
        public string CurrentLogFileName { get; set; } = "current.log";
        public string? WatchedDirectoryPath { get; private set; }
        public string? WatchedFileName { get; private set; }
        public string? LastOpenedFolderPath { get; private set; }
        public string LastClipboardText { get; private set; } = string.Empty;
        public Dictionary<string, IReadOnlyList<string>> LogLinesByPath { get; } = [];
        public Exception? ReadException { get; set; }

        public IDisposable WatchLogFile(string directoryPath, string fileName, Action<string> onChanged)
        {
            WatchedDirectoryPath = directoryPath;
            WatchedFileName = fileName;
            _onChanged = onChanged;
            return new CallbackDisposable(() => _onChanged = null);
        }

        public IReadOnlyList<string> ReadLogLines(string fullPath) =>
            ReadException is not null
                ? throw ReadException
                : LogLinesByPath.TryGetValue(fullPath, out var lines) ? lines : [];

        public void OpenFolder(string folderPath) => LastOpenedFolderPath = folderPath;

        public Task SetClipboardTextAsync(string text)
        {
            LastClipboardText = text;
            return Task.CompletedTask;
        }

        public void RaiseFileChanged(string fullPath) => _onChanged?.Invoke(fullPath);

        private sealed class CallbackDisposable(Action onDispose) : IDisposable
        {
            public void Dispose() => onDispose();
        }
    }
}
