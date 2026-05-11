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

using YouTubeMusicStreamer.Components.Pages.Home.Components;
using YouTubeMusicStreamer.Services.Startup;
using YouTubeMusicStreamer.Services.Twitch;

namespace YouTubeMusicStreamer.Tests.Components.Pages.Home;

public sealed class TwitchConnectionViewStateTests
{
    [Theory]
    [InlineData(TwitchAccountRole.Bot, "songbot", "streamer", "(songbot)")]
    [InlineData(TwitchAccountRole.Broadcaster, "songbot", "streamer", "(streamer)")]
    [InlineData(TwitchAccountRole.Bot, "   ", "streamer", "")]
    [InlineData(TwitchAccountRole.Broadcaster, "songbot", "", "")]
    public void GetEffectiveChatIdentitySuffix_UsesOnlyTheActiveRoleIdentity(
        TwitchAccountRole role,
        string? botUsername,
        string? broadcasterUsername,
        string expected)
    {
        var suffix = TwitchConnectionViewState.GetEffectiveChatIdentitySuffix(role, botUsername, broadcasterUsername);

        Assert.Equal(expected, suffix);
    }

    [Theory]
    [InlineData(InstanceRuntimeState.Starting, StartupPhase.NotStarted, true)]
    [InlineData(InstanceRuntimeState.Starting, StartupPhase.SettingsLoaded, true)]
    [InlineData(InstanceRuntimeState.Starting, StartupPhase.BackgroundSubsystemsStarted, false)]
    [InlineData(InstanceRuntimeState.Starting, StartupPhase.Ready, false)]
    [InlineData(InstanceRuntimeState.Ready, StartupPhase.SettingsLoaded, false)]
    [InlineData(InstanceRuntimeState.Unhealthy, StartupPhase.Failed, false)]
    public void IsStartupRestoringTwitch_OnlyAppliesBeforeBackgroundSubsystemStartup(
        InstanceRuntimeState state,
        StartupPhase phase,
        bool expected)
    {
        var health = new InstanceHealthState(
            1234,
            "launch-token",
            DateTime.UtcNow,
            DateTime.UtcNow,
            state,
            phase,
            "1.0.0");

        var restoring = TwitchConnectionViewState.IsStartupRestoringTwitch(health);

        Assert.Equal(expected, restoring);
    }
}
