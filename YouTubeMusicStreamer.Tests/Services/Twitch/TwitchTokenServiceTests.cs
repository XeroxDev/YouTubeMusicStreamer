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

public sealed class TwitchTokenServiceTests
{
    [Fact]
    public async Task ValidateAsync_ReturnsTrue_WhenApiAcceptsToken()
    {
        var client = new FakeTwitchApiClient
        {
            ValidateResult = true
        };
        var service = new TwitchTokenService(NullLogger<TwitchTokenService>.Instance, new FakeTwitchApiFactory(client));

        var result = await service.ValidateAsync("good-token");

        Assert.True(result);
        Assert.Equal("good-token", client.LastValidatedToken);
        Assert.Equal("good-token", client.LastAccessToken);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsFalse_WhenApiRejectsToken()
    {
        var client = new FakeTwitchApiClient
        {
            ValidateResult = false
        };
        var service = new TwitchTokenService(NullLogger<TwitchTokenService>.Instance, new FakeTwitchApiFactory(client));

        var result = await service.ValidateAsync("bad-token");

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsFalse_WhenApiThrows()
    {
        var client = new FakeTwitchApiClient
        {
            ValidateException = new InvalidOperationException("boom")
        };
        var service = new TwitchTokenService(NullLogger<TwitchTokenService>.Instance, new FakeTwitchApiFactory(client));

        var result = await service.ValidateAsync("exploding-token");

        Assert.False(result);
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
        public string? LastValidatedToken { get; private set; }
        public bool ValidateResult { get; set; }
        public Exception? ValidateException { get; set; }

        public Task<bool> ValidateAccessTokenAsync(string accessToken)
        {
            LastValidatedToken = accessToken;
            if (ValidateException is not null)
                throw ValidateException;

            return Task.FromResult(ValidateResult);
        }

        public Task<TwitchUserIdentity?> GetCurrentUserAsync() => Task.FromResult<TwitchUserIdentity?>(null);
        public Task<IReadOnlyList<TwitchRewardSnapshot>> GetRewardsAsync(string broadcasterId, IReadOnlyList<string>? rewardIds = null) => Task.FromResult<IReadOnlyList<TwitchRewardSnapshot>>([]);
        public Task<TwitchManagedReward> CreateManagedRewardAsync(string broadcasterId, TwitchManagedRewardDefinition definition) => throw new NotSupportedException();
        public Task<TwitchManagedReward> UpdateManagedRewardAsync(string broadcasterId, string rewardId, TwitchManagedRewardDefinition definition) => throw new NotSupportedException();
        public Task UpdateRedemptionStatusAsync(string broadcasterId, string rewardId, string redemptionId, TwitchRedemptionStatus status) => throw new NotSupportedException();
        public Task<IReadOnlyList<string>> CreateEventSubSubscriptionAsync(string type, string version, IReadOnlyDictionary<string, string> conditions, string sessionId) => throw new NotSupportedException();
        public Task DeleteEventSubSubscriptionAsync(string subscriptionId) => throw new NotSupportedException();
    }
}
