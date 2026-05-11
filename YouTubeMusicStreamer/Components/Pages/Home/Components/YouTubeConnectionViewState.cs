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

using YouTubeMusicStreamer.Services.YouTube;

namespace YouTubeMusicStreamer.Components.Pages.Home.Components;

internal static class YouTubeConnectionViewState
{
    public static bool ShouldReconnect(
        string enteredHost,
        int enteredPort,
        string currentHost,
        int currentPort,
        YtmAuthorizationStatus authorizationStatus)
    {
        var endpointChanged = !string.Equals(enteredHost, currentHost, StringComparison.OrdinalIgnoreCase) ||
                              enteredPort != currentPort;

        return authorizationStatus == YtmAuthorizationStatus.Authorized && !endpointChanged;
    }

    public static string GetEndpointStatusLabel(YtmEndpointStatus status) => status switch
    {
        YtmEndpointStatus.ValidCompanion => "Valid companion",
        YtmEndpointStatus.Unreachable => "Unreachable",
        YtmEndpointStatus.IncompatibleEndpoint => "Wrong endpoint",
        YtmEndpointStatus.NotConfigured => "Not configured",
        _ => "Unknown"
    };

    public static UiStatusTone GetEndpointStatusTone(YtmEndpointStatus status) => status switch
    {
        YtmEndpointStatus.ValidCompanion => UiStatusTone.Success,
        YtmEndpointStatus.Unreachable => UiStatusTone.Warning,
        YtmEndpointStatus.IncompatibleEndpoint => UiStatusTone.Danger,
        YtmEndpointStatus.NotConfigured => UiStatusTone.Neutral,
        _ => UiStatusTone.Info
    };

    public static string GetEndpointStatusTagClass(YtmEndpointStatus status) =>
        GetEndpointStatusTone(status).ToTagClass();

    public static string GetAuthorizationStatusLabel(YtmAuthorizationStatus status) => status switch
    {
        YtmAuthorizationStatus.NotConfigured => "Not configured",
        YtmAuthorizationStatus.AuthRequired => "Auth required",
        YtmAuthorizationStatus.Authorizing => "Authorizing",
        YtmAuthorizationStatus.Authorized => "Authorized",
        YtmAuthorizationStatus.AuthFailed => "Auth failed",
        _ => "Unknown"
    };

    public static UiStatusTone GetAuthorizationStatusTone(YtmAuthorizationStatus status) => status switch
    {
        YtmAuthorizationStatus.Authorized => UiStatusTone.Success,
        YtmAuthorizationStatus.Authorizing => UiStatusTone.Info,
        YtmAuthorizationStatus.AuthRequired => UiStatusTone.Warning,
        YtmAuthorizationStatus.AuthFailed => UiStatusTone.Danger,
        _ => UiStatusTone.Neutral
    };

    public static string GetAuthorizationStatusTagClass(YtmAuthorizationStatus status) =>
        GetAuthorizationStatusTone(status).ToTagClass();

    public static string GetConnectionStatusLabel(YtmConnectionStatus status) => status switch
    {
        YtmConnectionStatus.Connected => "Connected",
        YtmConnectionStatus.Connecting => "Connecting",
        YtmConnectionStatus.Retrying => "Retrying",
        YtmConnectionStatus.Degraded => "Degraded",
        YtmConnectionStatus.Error => "Error",
        _ => "Disconnected"
    };

    public static UiStatusTone GetConnectionStatusTone(YtmConnectionStatus status) => status switch
    {
        YtmConnectionStatus.Connected => UiStatusTone.Success,
        YtmConnectionStatus.Connecting or YtmConnectionStatus.Retrying => UiStatusTone.Info,
        YtmConnectionStatus.Degraded => UiStatusTone.Warning,
        YtmConnectionStatus.Error => UiStatusTone.Danger,
        _ => UiStatusTone.Neutral
    };

    public static string GetConnectionStatusTagClass(YtmConnectionStatus status) =>
        GetConnectionStatusTone(status).ToTagClass();
}
