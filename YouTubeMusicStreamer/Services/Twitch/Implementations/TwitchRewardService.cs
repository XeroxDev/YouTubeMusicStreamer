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

using Microsoft.Extensions.Logging;
using YouTubeMusicStreamer.Services.Twitch.Interfaces;

namespace YouTubeMusicStreamer.Services.Twitch.Implementations;

public sealed class TwitchRewardService(ILogger<TwitchRewardService> logger, ITwitchApiFactory apiFactory) : ITwitchRewardService
{
    public async Task<TwitchManagedReward?> GetRewardByIdAsync(string accessToken, string broadcasterId, string rewardId, CancellationToken cancellationToken = default)
    {
        var api = apiFactory.Create(accessToken);
        try
        {
            return (await api.GetRewardsAsync(broadcasterId, [rewardId])).FirstOrDefault() is { } reward
                ? MapReward(reward)
                : null;
        }
        catch (Exception ex)
        {
            logger.LogInformation(ex, "Managed Twitch reward {RewardId} could not be refreshed for broadcaster {BroadcasterId}. Treating it as missing.", rewardId, broadcasterId);
            return null;
        }
    }

    public async Task<TwitchManagedReward> CreateManagedRewardAsync(string accessToken, string broadcasterId, TwitchManagedRewardDefinition definition, CancellationToken cancellationToken = default)
    {
        var api = apiFactory.Create(accessToken);
        var reward = await api.CreateManagedRewardAsync(broadcasterId, definition);
        logger.LogInformation("Created managed Twitch reward {RewardId} ({RewardTitle})", reward.Id, reward.Title);
        return reward;
    }

    public async Task<TwitchManagedReward> UpdateManagedRewardAsync(string accessToken, string broadcasterId, string rewardId, TwitchManagedRewardDefinition definition, CancellationToken cancellationToken = default)
    {
        var api = apiFactory.Create(accessToken);
        var reward = await api.UpdateManagedRewardAsync(broadcasterId, rewardId, definition);
        logger.LogInformation("Updated managed Twitch reward {RewardId} ({RewardTitle})", reward.Id, reward.Title);
        return reward;
    }

    public async Task UpdateRedemptionStatusAsync(string accessToken, string broadcasterId, string rewardId, string redemptionId, TwitchRedemptionStatus status, CancellationToken cancellationToken = default)
    {
        var api = apiFactory.Create(accessToken);
        await api.UpdateRedemptionStatusAsync(broadcasterId, rewardId, redemptionId, status);
        logger.LogInformation("Updated Twitch redemption {RedemptionId} for reward {RewardId} to {Status}", redemptionId, rewardId, status);
    }

    private static TwitchManagedReward MapReward(TwitchRewardSnapshot reward) =>
        new(
            reward.Id,
            reward.Title,
            reward.Prompt,
            reward.Cost,
            reward.RequiresUserInput);
}
