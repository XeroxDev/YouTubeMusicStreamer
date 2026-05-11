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

using YouTubeMusicStreamer.Services.App.Persistence;
using YouTubeMusicStreamer.Services.Commands.Cooldowns;

namespace YouTubeMusicStreamer.Tests.Commands;

public class CooldownManagerTests
{
    [Fact]
    public void TryStart_AllowsZeroCooldown()
    {
        var manager = new CooldownManager();

        var started = manager.TryStart("request", "user-1", CommandCooldownScope.Global, 0, out var wait);

        Assert.True(started);
        Assert.Equal(0, wait);
    }

    [Fact]
    public void TryStart_AllowsRepeatedInvocation_WhenCooldownIsZero()
    {
        var manager = new CooldownManager();

        var first = manager.TryStart("request", "user-1", CommandCooldownScope.Global, 0, out var firstWait);
        var second = manager.TryStart("request", "user-1", CommandCooldownScope.Global, 0, out var secondWait);

        Assert.True(first);
        Assert.True(second);
        Assert.Equal(0, firstWait);
        Assert.Equal(0, secondWait);
    }

    [Fact]
    public void TryStart_BlocksRepeatedGlobalInvocation_DuringCooldown()
    {
        var manager = new CooldownManager();

        var first = manager.TryStart("request", "user-1", CommandCooldownScope.Global, 60, out var firstWait);
        var second = manager.TryStart("request", "user-2", CommandCooldownScope.Global, 60, out var secondWait);

        Assert.True(first);
        Assert.Equal(0, firstWait);
        Assert.False(second);
        Assert.InRange(secondWait, 1, 60);
    }

    [Fact]
    public void TryStart_AllowsDifferentUsers_WhenCooldownScopeIsPerUser()
    {
        var manager = new CooldownManager();

        var first = manager.TryStart("request", "user-1", CommandCooldownScope.PerUser, 60, out _);
        var second = manager.TryStart("request", "user-2", CommandCooldownScope.PerUser, 60, out var wait);

        Assert.True(first);
        Assert.True(second);
        Assert.Equal(0, wait);
    }

    [Fact]
    public void TryStart_BlocksRepeatedPerUserInvocation_ForSameUser()
    {
        var manager = new CooldownManager();

        var first = manager.TryStart("request", "user-1", CommandCooldownScope.PerUser, 60, out var firstWait);
        var second = manager.TryStart("request", "user-1", CommandCooldownScope.PerUser, 60, out var secondWait);

        Assert.True(first);
        Assert.Equal(0, firstWait);
        Assert.False(second);
        Assert.InRange(secondWait, 1, 60);
    }

    [Fact]
    public void TryStart_AllowsDifferentCommands_WhenCooldownScopeIsGlobal()
    {
        var manager = new CooldownManager();

        var first = manager.TryStart("request", "user-1", CommandCooldownScope.Global, 60, out _);
        var second = manager.TryStart("skip", "user-1", CommandCooldownScope.Global, 60, out var wait);

        Assert.True(first);
        Assert.True(second);
        Assert.Equal(0, wait);
    }

    [Fact]
    public void TryStart_AllowsDifferentCommands_WhenCooldownScopeIsPerUser()
    {
        var manager = new CooldownManager();

        var first = manager.TryStart("request", "user-1", CommandCooldownScope.PerUser, 60, out _);
        var second = manager.TryStart("skip", "user-1", CommandCooldownScope.PerUser, 60, out var wait);

        Assert.True(first);
        Assert.True(second);
        Assert.Equal(0, wait);
    }

    [Fact]
    public void TryStart_UsesIndependentBuckets_WhenScopeChangesForSameCommandAndUser()
    {
        var manager = new CooldownManager();

        var global = manager.TryStart("request", "user-1", CommandCooldownScope.Global, 60, out var globalWait);
        var perUser = manager.TryStart("request", "user-1", CommandCooldownScope.PerUser, 60, out var perUserWait);

        Assert.True(global);
        Assert.True(perUser);
        Assert.Equal(0, globalWait);
        Assert.Equal(0, perUserWait);
    }
}
