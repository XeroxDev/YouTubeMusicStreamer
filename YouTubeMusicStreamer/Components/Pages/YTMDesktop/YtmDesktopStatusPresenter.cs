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

using YouTubeMusicStreamer.Services.WebSocket;
using YouTubeMusicStreamer.Services.YouTube;

namespace YouTubeMusicStreamer.Components.Pages.YTMDesktop;

internal static class YtmDesktopStatusPresenter
{
    public static WidgetServerStatus GetEffectiveServerStatus(WidgetServerState serverState, YtmCompanionSessionState companionState)
    {
        return serverState.Status == WidgetServerStatus.Running &&
               companionState.ConnectionStatus != YtmConnectionStatus.Connected
            ? WidgetServerStatus.Degraded
            : serverState.Status;
    }

    public static string GetServerStatusLabel(WidgetServerStatus effectiveServerStatus) => effectiveServerStatus.ToString();

    public static UiStatusTone GetServerStatusTone(WidgetServerStatus effectiveServerStatus) => effectiveServerStatus switch
    {
        WidgetServerStatus.Running => UiStatusTone.Success,
        WidgetServerStatus.Starting => UiStatusTone.Info,
        WidgetServerStatus.Degraded => UiStatusTone.Warning,
        WidgetServerStatus.Error => UiStatusTone.Danger,
        _ => UiStatusTone.Neutral
    };

    public static string GetServerStatusTagClass(WidgetServerStatus effectiveServerStatus) =>
        GetServerStatusTone(effectiveServerStatus).ToTagClass();

    public static string? GetServerStatusMessage(WidgetServerState serverState, WidgetServerStatus effectiveServerStatus)
    {
        if (!string.IsNullOrWhiteSpace(serverState.ErrorMessage))
            return serverState.ErrorMessage;

        return effectiveServerStatus == WidgetServerStatus.Degraded
            ? "The widget server is running, but some live data is currently unavailable."
            : null;
    }

    public static bool IsServerStatusProblem(WidgetServerStatus effectiveServerStatus) =>
        effectiveServerStatus is WidgetServerStatus.Degraded or WidgetServerStatus.Error;

    public static bool IsAudioConfiguredButInactive(WidgetServerState serverState) =>
        serverState.Configuration.AudioEnabled &&
        serverState.AudioState.Status == AudioCaptureStatus.Disabled;

    public static string GetAudioStatusLabel(WidgetServerState serverState) =>
        IsAudioConfiguredButInactive(serverState)
            ? "Configured"
            : serverState.AudioState.Status.ToString();

    public static UiStatusTone GetAudioStatusTone(WidgetServerState serverState) => serverState.AudioState.Status switch
    {
        _ when IsAudioConfiguredButInactive(serverState) => UiStatusTone.Warning,
        AudioCaptureStatus.Capturing => UiStatusTone.Success,
        AudioCaptureStatus.Starting => UiStatusTone.Info,
        AudioCaptureStatus.Error => UiStatusTone.Danger,
        AudioCaptureStatus.Disabled => UiStatusTone.Neutral,
        _ => UiStatusTone.Warning
    };

    public static string GetAudioStatusTagClass(WidgetServerState serverState) =>
        GetAudioStatusTone(serverState).ToTagClass();

    public static string? GetAudioStatusMessage(WidgetServerState serverState)
    {
        if (!string.IsNullOrWhiteSpace(serverState.AudioState.ErrorMessage))
            return serverState.AudioState.ErrorMessage;

        if (IsAudioConfiguredButInactive(serverState))
            return "Audio streaming is enabled, but capture is not currently active.";

        return serverState.AudioState.Status == AudioCaptureStatus.Disabled
            ? "Audio streaming is currently disabled."
            : null;
    }

    public static bool IsAudioStatusProblem(WidgetServerState serverState) =>
        serverState.AudioState.Status == AudioCaptureStatus.Error;

}
