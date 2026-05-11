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

using TwitchLib.Api;
using TwitchLib.Api.Core.Enums;
using TwitchLib.Api.Helix.Models.ChannelPoints;
using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;
using TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomReward;
using TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomRewardRedemptionStatus;
using YouTubeMusicStreamer.Services.Twitch.Interfaces;

namespace YouTubeMusicStreamer.Services.Twitch.Implementations;

public sealed class TwitchApiFactory : ITwitchApiFactory
{
    public ITwitchApiClient Create(string accessToken)
    {
        var api = new TwitchAPI();
        api.Settings.ClientId = GeneratedBuildInfo.TwitchClientId;
        api.Settings.AccessToken = accessToken;
        return new TwitchApiClient(api, accessToken);
    }
}

internal sealed class TwitchApiClient(TwitchAPI api, string accessToken) : ITwitchApiClient
{
    public async Task<bool> ValidateAccessTokenAsync(string token) =>
        await api.Auth.ValidateAccessTokenAsync(token) is not null;

    public async Task<TwitchUserIdentity?> GetCurrentUserAsync()
    {
        var response = await api.Helix.Users.GetUsersAsync();
        var user = response?.Users.FirstOrDefault();
        return user is null
            ? null
            : new TwitchUserIdentity(user.Id, user.Login, user.DisplayName, user.ProfileImageUrl);
    }

    public async Task<IReadOnlyList<TwitchRewardSnapshot>> GetRewardsAsync(string broadcasterId, IReadOnlyList<string>? rewardIds = null)
    {
        var ids = rewardIds is { Count: > 0 } ? rewardIds.ToList() : null;
        var response = await api.Helix.ChannelPoints.GetCustomRewardAsync(broadcasterId, ids, false, accessToken);
        return response?.Data?
            .Select(MapReward)
            .OrderBy(static reward => reward.Title, StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];
    }

    public async Task<TwitchManagedReward> CreateManagedRewardAsync(string broadcasterId, TwitchManagedRewardDefinition definition)
    {
        var response = await api.Helix.ChannelPoints.CreateCustomRewardsAsync(
            broadcasterId,
            new CreateCustomRewardsRequest
            {
                Title = definition.Title,
                Cost = checked((int)definition.Cost),
                Prompt = definition.Prompt,
                IsUserInputRequired = definition.RequiresUserInput,
                IsEnabled = true,
                ShouldRedemptionsSkipRequestQueue = false
            },
            accessToken);

        var reward = response?.Data?.FirstOrDefault()
                     ?? throw new InvalidOperationException("Twitch did not return the created managed reward.");

        return MapManagedReward(reward);
    }

    public async Task<TwitchManagedReward> UpdateManagedRewardAsync(string broadcasterId, string rewardId, TwitchManagedRewardDefinition definition)
    {
        var response = await api.Helix.ChannelPoints.UpdateCustomRewardAsync(
            broadcasterId,
            rewardId,
            new UpdateCustomRewardRequest
            {
                Title = definition.Title,
                Cost = checked((int)definition.Cost),
                Prompt = definition.Prompt,
                IsUserInputRequired = definition.RequiresUserInput,
                IsEnabled = true,
                ShouldRedemptionsSkipRequestQueue = false
            },
            accessToken);

        var reward = response?.Data?.FirstOrDefault()
                     ?? throw new InvalidOperationException("Twitch did not return the updated managed reward.");

        return MapManagedReward(reward);
    }

    public Task UpdateRedemptionStatusAsync(string broadcasterId, string rewardId, string redemptionId, TwitchRedemptionStatus status) =>
        api.Helix.ChannelPoints.UpdateRedemptionStatusAsync(
            broadcasterId,
            rewardId,
            [redemptionId],
            new UpdateCustomRewardRedemptionStatusRequest
            {
                Status = status == TwitchRedemptionStatus.Fulfilled
                    ? CustomRewardRedemptionStatus.FULFILLED
                    : CustomRewardRedemptionStatus.CANCELED
            },
            accessToken);

    public async Task<IReadOnlyList<string>> CreateEventSubSubscriptionAsync(string type, string version, IReadOnlyDictionary<string, string> conditions, string sessionId)
    {
        var response = await api.Helix.EventSub.CreateEventSubSubscriptionAsync(
            type,
            version,
            conditions.ToDictionary(static pair => pair.Key, static pair => pair.Value),
            EventSubTransportMethod.Websocket,
            sessionId);

        return response?.Subscriptions?
            .Where(static subscription => !string.IsNullOrWhiteSpace(subscription.Id))
            .Select(static subscription => subscription.Id)
            .ToList() ?? [];
    }

    public Task DeleteEventSubSubscriptionAsync(string subscriptionId) =>
        api.Helix.EventSub.DeleteEventSubSubscriptionAsync(subscriptionId);

    private static TwitchRewardSnapshot MapReward(CustomReward reward) =>
        new(
            reward.Id,
            reward.Title,
            reward.Prompt,
            checked((uint)reward.Cost),
            reward.IsUserInputRequired);

    private static TwitchManagedReward MapManagedReward(CustomReward reward) =>
        new(
            reward.Id,
            reward.Title,
            reward.Prompt,
            checked((uint)reward.Cost),
            reward.IsUserInputRequired);
}
