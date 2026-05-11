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

using YouTubeMusicStreamer.Services.Startup;
using YouTubeMusicStreamer.Services.Twitch;

namespace YouTubeMusicStreamer.Components.Pages.Home.Components;

internal static class TwitchConnectionViewState
{
    public static string GetEffectiveChatIdentitySuffix(
        TwitchAccountRole effectiveChatRole,
        string? botUsername,
        string? broadcasterUsername)
    {
        return effectiveChatRole switch
        {
            TwitchAccountRole.Bot when !string.IsNullOrWhiteSpace(botUsername) => $"({botUsername})",
            TwitchAccountRole.Broadcaster when !string.IsNullOrWhiteSpace(broadcasterUsername) => $"({broadcasterUsername})",
            _ => string.Empty
        };
    }

    public static bool IsStartupRestoringTwitch(InstanceHealthState health) =>
        health.State == InstanceRuntimeState.Starting &&
        health.Phase < StartupPhase.BackgroundSubsystemsStarted;
}
