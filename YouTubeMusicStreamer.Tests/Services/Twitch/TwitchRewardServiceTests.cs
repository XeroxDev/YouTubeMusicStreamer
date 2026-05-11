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

public sealed class TwitchRewardServiceTests
{
    [Fact]
    public async Task GetRewardByIdAsync_ReturnsNull_WhenApiLookupThrows()
    {
        var client = new FakeTwitchApiClient
        {
            GetRewardsException = new InvalidOperationException("boom")
        };
        var service = new TwitchRewardService(NullLogger<TwitchRewardService>.Instance, new FakeTwitchApiFactory(client));

        var result = await service.GetRewardByIdAsync("token", "broadcaster", "reward-1");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetRewardByIdAsync_MapsSingleRewardSnapshot()
    {
        var client = new FakeTwitchApiClient();
        client.Rewards["broadcaster"] = [new TwitchRewardSnapshot("reward-1", "Reward", "Prompt", 250, true)];
        var service = new TwitchRewardService(NullLogger<TwitchRewardService>.Instance, new FakeTwitchApiFactory(client));

        var result = await service.GetRewardByIdAsync("token", "broadcaster", "reward-1");

        Assert.NotNull(result);
        Assert.Equal("reward-1", result!.Id);
        Assert.Equal("Reward", result.Title);
        Assert.Equal((uint)250, result.Cost);
        Assert.True(result.RequiresUserInput);
        Assert.Equal(["reward-1"], client.LastRequestedRewardIds);
    }

    [Fact]
    public async Task CreateManagedRewardAsync_ForwardsDefinition_AndReturnsCreatedReward()
    {
        var expected = new TwitchManagedReward("reward-2", "Created", "Prompt", 500, false);
        var client = new FakeTwitchApiClient
        {
            CreatedReward = expected
        };
        var definition = new TwitchManagedRewardDefinition("Created", "Prompt", 500, false);
        var service = new TwitchRewardService(NullLogger<TwitchRewardService>.Instance, new FakeTwitchApiFactory(client));

        var result = await service.CreateManagedRewardAsync("token", "broadcaster", definition);

        Assert.Equal(expected, result);
        Assert.Equal("broadcaster", client.LastCreateBroadcasterId);
        Assert.Equal(definition, client.LastCreateDefinition);
    }

    [Fact]
    public async Task UpdateManagedRewardAsync_ForwardsDefinition_AndRewardId()
    {
        var expected = new TwitchManagedReward("reward-3", "Updated", "Prompt", 750, true);
        var client = new FakeTwitchApiClient
        {
            UpdatedReward = expected
        };
        var definition = new TwitchManagedRewardDefinition("Updated", "Prompt", 750, true);
        var service = new TwitchRewardService(NullLogger<TwitchRewardService>.Instance, new FakeTwitchApiFactory(client));

        var result = await service.UpdateManagedRewardAsync("token", "broadcaster", "reward-3", definition);

        Assert.Equal(expected, result);
        Assert.Equal("broadcaster", client.LastUpdateBroadcasterId);
        Assert.Equal("reward-3", client.LastUpdateRewardId);
        Assert.Equal(definition, client.LastUpdateDefinition);
    }

    [Theory]
    [InlineData(TwitchRedemptionStatus.Fulfilled)]
    [InlineData(TwitchRedemptionStatus.Canceled)]
    public async Task UpdateRedemptionStatusAsync_ForwardsStatus(TwitchRedemptionStatus status)
    {
        var client = new FakeTwitchApiClient();
        var service = new TwitchRewardService(NullLogger<TwitchRewardService>.Instance, new FakeTwitchApiFactory(client));

        await service.UpdateRedemptionStatusAsync("token", "broadcaster", "reward-4", "redemption-1", status);

        Assert.Equal(("broadcaster", "reward-4", "redemption-1", status), client.LastRedemptionUpdate);
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
        public Exception? GetRewardsException { get; set; }
        public Dictionary<string, IReadOnlyList<TwitchRewardSnapshot>> Rewards { get; } = new(StringComparer.Ordinal);
        public IReadOnlyList<string>? LastRequestedRewardIds { get; private set; }
        public TwitchManagedReward CreatedReward { get; set; } = new("created", "Created", null, 1000, false);
        public TwitchManagedReward UpdatedReward { get; set; } = new("updated", "Updated", null, 1000, false);
        public string? LastCreateBroadcasterId { get; private set; }
        public TwitchManagedRewardDefinition? LastCreateDefinition { get; private set; }
        public string? LastUpdateBroadcasterId { get; private set; }
        public string? LastUpdateRewardId { get; private set; }
        public TwitchManagedRewardDefinition? LastUpdateDefinition { get; private set; }
        public (string BroadcasterId, string RewardId, string RedemptionId, TwitchRedemptionStatus Status)? LastRedemptionUpdate { get; private set; }

        public Task<bool> ValidateAccessTokenAsync(string accessToken) => Task.FromResult(true);

        public Task<TwitchUserIdentity?> GetCurrentUserAsync() => Task.FromResult<TwitchUserIdentity?>(null);

        public Task<IReadOnlyList<TwitchRewardSnapshot>> GetRewardsAsync(string broadcasterId, IReadOnlyList<string>? rewardIds = null)
        {
            if (GetRewardsException is not null)
                throw GetRewardsException;

            LastRequestedRewardIds = rewardIds;
            if (!Rewards.TryGetValue(broadcasterId, out var rewards))
                return Task.FromResult<IReadOnlyList<TwitchRewardSnapshot>>([]);

            if (rewardIds is null || rewardIds.Count == 0)
                return Task.FromResult(rewards);

            return Task.FromResult<IReadOnlyList<TwitchRewardSnapshot>>(rewards.Where(reward => rewardIds.Contains(reward.Id, StringComparer.Ordinal)).ToList());
        }

        public Task<TwitchManagedReward> CreateManagedRewardAsync(string broadcasterId, TwitchManagedRewardDefinition definition)
        {
            LastCreateBroadcasterId = broadcasterId;
            LastCreateDefinition = definition;
            return Task.FromResult(CreatedReward);
        }

        public Task<TwitchManagedReward> UpdateManagedRewardAsync(string broadcasterId, string rewardId, TwitchManagedRewardDefinition definition)
        {
            LastUpdateBroadcasterId = broadcasterId;
            LastUpdateRewardId = rewardId;
            LastUpdateDefinition = definition;
            return Task.FromResult(UpdatedReward);
        }

        public Task UpdateRedemptionStatusAsync(string broadcasterId, string rewardId, string redemptionId, TwitchRedemptionStatus status)
        {
            LastRedemptionUpdate = (broadcasterId, rewardId, redemptionId, status);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<string>> CreateEventSubSubscriptionAsync(string type, string version, IReadOnlyDictionary<string, string> conditions, string sessionId) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task DeleteEventSubSubscriptionAsync(string subscriptionId) => Task.CompletedTask;
    }
}
