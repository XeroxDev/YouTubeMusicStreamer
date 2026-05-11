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

using XeroxDev.YTMDesktop.Companion.Enums;
using YouTubeMusicStreamer.Components;
using YouTubeMusicStreamer.Components.Pages.YTMDesktop;
using YouTubeMusicStreamer.Services.WebSocket;
using YouTubeMusicStreamer.Services.YouTube;

namespace YouTubeMusicStreamer.Tests.Components.Pages.YTMDesktop;

public sealed class YtmDesktopStatusPresenterTests
{
    [Fact]
    public void GetEffectiveServerStatus_DegradesRunningServer_WhenCompanionIsNotConnected()
    {
        var serverState = CreateServerState(WidgetServerStatus.Running, AudioCaptureStatus.Disabled, audioEnabled: false);
        var companionState = CreateCompanionState(YtmConnectionStatus.Disconnected);

        var status = YtmDesktopStatusPresenter.GetEffectiveServerStatus(serverState, companionState);

        Assert.Equal(WidgetServerStatus.Degraded, status);
        Assert.Equal(UiStatusTone.Warning, YtmDesktopStatusPresenter.GetServerStatusTone(status));
        Assert.True(YtmDesktopStatusPresenter.IsServerStatusProblem(status));
        Assert.False(string.IsNullOrWhiteSpace(YtmDesktopStatusPresenter.GetServerStatusMessage(serverState, status)));
    }

    [Fact]
    public void GetEffectiveServerStatus_KeepsRunningServer_WhenCompanionIsConnected()
    {
        var serverState = CreateServerState(WidgetServerStatus.Running, AudioCaptureStatus.Disabled, audioEnabled: false);
        var companionState = CreateCompanionState(YtmConnectionStatus.Connected);

        var status = YtmDesktopStatusPresenter.GetEffectiveServerStatus(serverState, companionState);

        Assert.Equal(WidgetServerStatus.Running, status);
        Assert.Equal(UiStatusTone.Success, YtmDesktopStatusPresenter.GetServerStatusTone(status));
        Assert.False(YtmDesktopStatusPresenter.IsServerStatusProblem(status));
        Assert.Null(YtmDesktopStatusPresenter.GetServerStatusMessage(serverState, status));
    }

    [Fact]
    public void GetServerStatusMessage_PrefersExplicitServerError()
    {
        var serverState = CreateServerState(
            WidgetServerStatus.Error,
            AudioCaptureStatus.Disabled,
            audioEnabled: false,
            serverError: "port unavailable");

        Assert.Equal(
            "port unavailable",
            YtmDesktopStatusPresenter.GetServerStatusMessage(serverState, WidgetServerStatus.Error));
    }

    [Fact]
    public void AudioStatus_ShowsConfiguredWarning_WhenAudioIsEnabledButDisabled()
    {
        var serverState = CreateServerState(WidgetServerStatus.Running, AudioCaptureStatus.Disabled, audioEnabled: true);

        Assert.True(YtmDesktopStatusPresenter.IsAudioConfiguredButInactive(serverState));
        Assert.Equal(UiStatusTone.Warning, YtmDesktopStatusPresenter.GetAudioStatusTone(serverState));
        Assert.False(string.IsNullOrWhiteSpace(YtmDesktopStatusPresenter.GetAudioStatusMessage(serverState)));
        Assert.False(YtmDesktopStatusPresenter.IsAudioStatusProblem(serverState));
    }

    [Fact]
    public void AudioStatus_ShowsErrorAsProblemAndPrefersErrorMessage()
    {
        var serverState = CreateServerState(
            WidgetServerStatus.Running,
            AudioCaptureStatus.Error,
            audioEnabled: true,
            audioError: "device gone");

        Assert.Equal(UiStatusTone.Danger, YtmDesktopStatusPresenter.GetAudioStatusTone(serverState));
        Assert.Equal("device gone", YtmDesktopStatusPresenter.GetAudioStatusMessage(serverState));
        Assert.True(YtmDesktopStatusPresenter.IsAudioStatusProblem(serverState));
    }

    [Fact]
    public void AudioStatus_ShowsDisabledMessage_WhenAudioIsDisabled()
    {
        var serverState = CreateServerState(WidgetServerStatus.Running, AudioCaptureStatus.Disabled, audioEnabled: false);

        Assert.False(YtmDesktopStatusPresenter.IsAudioConfiguredButInactive(serverState));
        Assert.Equal(UiStatusTone.Neutral, YtmDesktopStatusPresenter.GetAudioStatusTone(serverState));
        Assert.False(string.IsNullOrWhiteSpace(YtmDesktopStatusPresenter.GetAudioStatusMessage(serverState)));
    }

    private static WidgetServerState CreateServerState(
        WidgetServerStatus status,
        AudioCaptureStatus audioStatus,
        bool audioEnabled,
        string? serverError = null,
        string? audioError = null)
    {
        return new WidgetServerState(
            new WidgetServerConfiguration(9863, audioEnabled, "device-1"),
            status,
            0,
            null,
            new AudioSubsystemState(audioStatus, "device-1", audioError),
            serverError);
    }

    private static YtmCompanionSessionState CreateCompanionState(YtmConnectionStatus connectionStatus)
    {
        return new YtmCompanionSessionState(
            new YtmEndpointConfiguration("127.0.0.1", 9863),
            YtmEndpointStatus.ValidCompanion,
            YtmAuthorizationStatus.Authorized,
            connectionStatus,
            ESocketState.Connected,
            null,
            null,
            true,
            null);
    }
}
