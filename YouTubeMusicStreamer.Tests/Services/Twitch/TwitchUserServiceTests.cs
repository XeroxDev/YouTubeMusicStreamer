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

using Microsoft.Extensions.Logging.Abstractions;
using YouTubeMusicStreamer.Services.Twitch;
using YouTubeMusicStreamer.Services.Twitch.Implementations;
using YouTubeMusicStreamer.Services.Twitch.Interfaces;

namespace YouTubeMusicStreamer.Tests.Services.Twitch;

public sealed class TwitchUserServiceTests
{
    [Fact]
    public async Task GetCurrentUserAsync_ReturnsUser_WhenApiProvidesOne()
    {
        var expected = new TwitchUserIdentity("user-id", "login", "Display", "avatar.png");
        var client = new FakeTwitchApiClient
        {
            CurrentUser = expected
        };
        var service = new TwitchUserService(NullLogger<TwitchUserService>.Instance, new FakeTwitchApiFactory(client));

        var result = await service.GetCurrentUserAsync("token");

        Assert.Equal(expected, result);
        Assert.Equal("token", client.LastAccessToken);
    }

    [Fact]
    public async Task GetCurrentUserAsync_ReturnsNull_WhenApiReturnsNoUser()
    {
        var service = new TwitchUserService(NullLogger<TwitchUserService>.Instance, new FakeTwitchApiFactory(new FakeTwitchApiClient()));

        var result = await service.GetCurrentUserAsync("token");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetRewardsAsync_ReturnsEmpty_WhenChannelIdIsBlank()
    {
        var client = new FakeTwitchApiClient();
        var service = new TwitchUserService(NullLogger<TwitchUserService>.Instance, new FakeTwitchApiFactory(client));

        var result = await service.GetRewardsAsync("token", "   ");

        Assert.Empty(result);
        Assert.Null(client.LastRewardsBroadcasterId);
    }

    [Fact]
    public async Task GetRewardsAsync_ReturnsRewards_WhenApiSucceeds()
    {
        var rewards = new List<TwitchRewardSnapshot>
        {
            new("reward-1", "Alpha", "Prompt A", 100, false),
            new("reward-2", "Beta", "Prompt B", 200, true)
        };
        var client = new FakeTwitchApiClient
        {
            Rewards = rewards
        };
        var service = new TwitchUserService(NullLogger<TwitchUserService>.Instance, new FakeTwitchApiFactory(client));

        var result = await service.GetRewardsAsync("token", "channel-id");

        Assert.Equal(rewards, result);
        Assert.Equal("channel-id", client.LastRewardsBroadcasterId);
    }

    [Fact]
    public async Task GetRewardsAsync_ReturnsEmpty_WhenApiThrows()
    {
        var client = new FakeTwitchApiClient
        {
            RewardsException = new InvalidOperationException("boom")
        };
        var service = new TwitchUserService(NullLogger<TwitchUserService>.Instance, new FakeTwitchApiFactory(client));

        var result = await service.GetRewardsAsync("token", "channel-id");

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetRewardsAsync_ReturnsEmpty_WhenApiReturnsNoRewards()
    {
        var client = new FakeTwitchApiClient
        {
            Rewards = []
        };
        var service = new TwitchUserService(NullLogger<TwitchUserService>.Instance, new FakeTwitchApiFactory(client));

        var result = await service.GetRewardsAsync("token", "channel-id");

        Assert.Empty(result);
        Assert.Equal("channel-id", client.LastRewardsBroadcasterId);
    }

    private sealed class FakeTwitchApiFactory(FakeTwitchApiClient client) : ITwitchApiFactory
    {
        public ITwitchApiClient Create(string accessToken)
        {
            client.LastAccessToken = accessToken;
            return client;
        }
    }

    private sealed class FakeTwitchApiClient : ITwitchApiClient
    {
        public string? LastAccessToken { get; set; }
        public TwitchUserIdentity? CurrentUser { get; set; }
        public IReadOnlyList<TwitchRewardSnapshot> Rewards { get; set; } = [];
        public Exception? RewardsException { get; set; }
        public string? LastRewardsBroadcasterId { get; private set; }

        public Task<bool> ValidateAccessTokenAsync(string accessToken) => throw new NotSupportedException();

        public Task<TwitchUserIdentity?> GetCurrentUserAsync() => Task.FromResult(CurrentUser);

        public Task<IReadOnlyList<TwitchRewardSnapshot>> GetRewardsAsync(string broadcasterId, IReadOnlyList<string>? rewardIds = null)
        {
            LastRewardsBroadcasterId = broadcasterId;
            if (RewardsException is not null)
                throw RewardsException;

            return Task.FromResult(Rewards);
        }

        public Task<TwitchManagedReward> CreateManagedRewardAsync(string broadcasterId, TwitchManagedRewardDefinition definition) => throw new NotSupportedException();
        public Task<TwitchManagedReward> UpdateManagedRewardAsync(string broadcasterId, string rewardId, TwitchManagedRewardDefinition definition) => throw new NotSupportedException();
        public Task UpdateRedemptionStatusAsync(string broadcasterId, string rewardId, string redemptionId, TwitchRedemptionStatus status) => throw new NotSupportedException();
        public Task<IReadOnlyList<string>> CreateEventSubSubscriptionAsync(string type, string version, IReadOnlyDictionary<string, string> conditions, string sessionId) => throw new NotSupportedException();
        public Task DeleteEventSubSubscriptionAsync(string subscriptionId) => throw new NotSupportedException();
    }
}
