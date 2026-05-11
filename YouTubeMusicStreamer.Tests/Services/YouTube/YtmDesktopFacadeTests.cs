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
using XeroxDev.YTMDesktop.Companion.Enums;
using YouTubeMusicStreamer.Services.App;
using YouTubeMusicStreamer.Services.App.Persistence;
using YouTubeMusicStreamer.Services.WebSocket;
using YouTubeMusicStreamer.Services.YouTube;
using YouTubeMusicStreamer.Tests.TestSupport;

namespace YouTubeMusicStreamer.Tests.Services.YouTube;

public sealed class YtmDesktopFacadeTests
{
    [Fact]
    public async Task Initialize_LoadsCurrentSettingsAndState_AndIsIdempotent()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        await harness.Settings.InitializeAsync();
        await harness.Settings.SaveYouTubeSettingsAsync(new YouTubeSettingsSnapshot("127.0.0.1", 9863, true, 8090, true, "device-1"));

        var widget = new FakeWidgetServerController
        {
            State = new WidgetServerState(
                new WidgetServerConfiguration(8090, true, "device-1"),
                WidgetServerStatus.Running,
                2,
                null,
                new AudioSubsystemState(AudioCaptureStatus.Capturing, "device-1", null),
                null)
        };
        var youTube = new FakeYouTubeStatusSource
        {
            SessionState = CreateCompanionState(YtmConnectionStatus.Connected, "OK")
        };

        using var facade = new YtmDesktopFacade(harness.Settings, widget, youTube);

        facade.Initialize();
        facade.Initialize();

        Assert.True(facade.Settings.AutoStartServer);
        Assert.Equal(8090, facade.Settings.PublicPort);
        Assert.True(facade.ServerState.Configuration.AudioEnabled);
        Assert.Equal(YtmConnectionStatus.Connected, facade.CompanionState.ConnectionStatus);

        var stateChangedCount = 0;
        facade.StateChanged += (_, _) => stateChangedCount++;

        youTube.RaisePropertyChanged(nameof(IYouTubeStatusSource.SessionState));

        Assert.Equal(1, stateChangedCount);
    }

    [Fact]
    public async Task SaveServerSettingsAsync_PersistsSettings_AndAppliesServerConfiguration()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        await harness.Settings.InitializeAsync();
        var widget = new FakeWidgetServerController();
        var youTube = new FakeYouTubeStatusSource();

        using var facade = new YtmDesktopFacade(harness.Settings, widget, youTube);

        await facade.SaveServerSettingsAsync(true, 8123, true, "device-2");

        var saved = harness.Settings.GetYouTubeSettings();
        Assert.True(saved.AutoStartServer);
        Assert.Equal(8123, saved.PublicPort);
        Assert.True(saved.AllowAudioCapture);
        Assert.Equal("device-2", saved.AudioCaptureDevice);

        Assert.NotNull(widget.AppliedConfiguration);
        Assert.Equal(8123, widget.AppliedConfiguration!.PublicPort);
        Assert.True(widget.AppliedConfiguration.AllowAudioCapture);
        Assert.Equal("device-2", widget.AppliedConfiguration.AudioCaptureDevice);
    }

    [Fact]
    public async Task SaveServerSettingsAsync_PersistsSettings_EvenWhenApplyingWidgetConfigurationFails()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        await harness.Settings.InitializeAsync();
        var widget = new FakeWidgetServerController
        {
            ApplyConfigurationException = new InvalidOperationException("apply failed")
        };
        var youTube = new FakeYouTubeStatusSource();

        using var facade = new YtmDesktopFacade(harness.Settings, widget, youTube);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => facade.SaveServerSettingsAsync(true, 8123, true, "device-2"));

        Assert.Equal("apply failed", exception.Message);
        var saved = harness.Settings.GetYouTubeSettings();
        Assert.True(saved.AutoStartServer);
        Assert.Equal(8123, saved.PublicPort);
        Assert.True(saved.AllowAudioCapture);
        Assert.Equal("device-2", saved.AudioCaptureDevice);
    }

    [Fact]
    public async Task ToggleServerAsync_StartsServer_WhenNotRunning()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        await harness.Settings.InitializeAsync();
        var widget = new FakeWidgetServerController { IsRunning = false };
        var youTube = new FakeYouTubeStatusSource();

        using var facade = new YtmDesktopFacade(harness.Settings, widget, youTube);

        await facade.ToggleServerAsync();

        Assert.Equal(1, widget.StartCallCount);
        Assert.Equal(0, widget.StopCallCount);
    }

    [Fact]
    public async Task ToggleServerAsync_StopsServer_WhenRunning()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        await harness.Settings.InitializeAsync();
        var widget = new FakeWidgetServerController { IsRunning = true };
        var youTube = new FakeYouTubeStatusSource();

        using var facade = new YtmDesktopFacade(harness.Settings, widget, youTube);

        await facade.ToggleServerAsync();

        Assert.Equal(0, widget.StartCallCount);
        Assert.Equal(1, widget.StopCallCount);
    }

    [Fact]
    public async Task StateChanged_RaisesForWidgetAndYouTubeEvents_AndStopsAfterDispose()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        await harness.Settings.InitializeAsync();
        var widget = new FakeWidgetServerController();
        var youTube = new FakeYouTubeStatusSource();
        var facade = new YtmDesktopFacade(harness.Settings, widget, youTube);
        var stateChangedCount = 0;
        facade.StateChanged += (_, _) => stateChangedCount++;

        facade.Initialize();

        widget.RaiseStateChanged(widget.State with { Status = WidgetServerStatus.Running });
        youTube.RaisePropertyChanged(nameof(IYouTubeStatusSource.Error));

        Assert.Equal(2, stateChangedCount);

        facade.Dispose();
        widget.RaiseStateChanged(widget.State with { Status = WidgetServerStatus.Error });
        youTube.RaisePropertyChanged(nameof(IYouTubeStatusSource.SessionState));

        Assert.Equal(2, stateChangedCount);
    }

    private static YtmCompanionSessionState CreateCompanionState(YtmConnectionStatus status, string? error) =>
        new(
            new YtmEndpointConfiguration("127.0.0.1", 9863),
            YtmEndpointStatus.ValidCompanion,
            YtmAuthorizationStatus.Authorized,
            status,
            ESocketState.Connected,
            error,
            null,
            true,
            null);

    private sealed class FakeWidgetServerController : IWidgetServerController
    {
        public WidgetServerState State { get; set; } = new(
            new WidgetServerConfiguration(8080, false, string.Empty),
            WidgetServerStatus.Stopped,
            0,
            null,
            new AudioSubsystemState(AudioCaptureStatus.Disabled, null, null),
            null);

        public bool IsRunning { get; set; }
        public event EventHandler<WidgetServerState> StateChanged = delegate { };
        public int StartCallCount { get; private set; }
        public int StopCallCount { get; private set; }
        public YouTubeSettingsSnapshot? AppliedConfiguration { get; private set; }
        public Exception? ApplyConfigurationException { get; set; }

        public Task StartAsync()
        {
            StartCallCount++;
            IsRunning = true;
            return Task.CompletedTask;
        }

        public Task ApplyConfigurationAsync(YouTubeSettingsSnapshot settings, CancellationToken cancellationToken = default)
        {
            if (ApplyConfigurationException is not null)
                return Task.FromException(ApplyConfigurationException);

            AppliedConfiguration = settings;
            return Task.CompletedTask;
        }

        public void Stop()
        {
            StopCallCount++;
            IsRunning = false;
        }

        public void RaiseStateChanged(WidgetServerState state)
        {
            State = state;
            StateChanged(this, state);
        }
    }

    private sealed class FakeYouTubeStatusSource : IYouTubeStatusSource
    {
        public YtmCompanionSessionState SessionState { get; set; } = CreateCompanionState(YtmConnectionStatus.Disconnected, null);
        public string Error { get; set; } = string.Empty;
        public event PropertyChangedEventHandler? PropertyChanged;

        public void RaisePropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
