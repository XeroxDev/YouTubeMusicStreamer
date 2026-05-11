// This file is part of YouTubeMusicStreamer.
// Copyright (C) 2025 Dominic Ris
// 
// YouTubeMusicStreamer is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as published
// by the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version (the "AGPLv3").
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

public sealed class TwitchUserService(ILogger<TwitchUserService> logger, ITwitchApiFactory apiFactory) : ITwitchUserService
{
    public async Task<TwitchUserIdentity?> GetCurrentUserAsync(string accessToken)
    {
        var api = apiFactory.Create(accessToken);
        var user = await api.GetCurrentUserAsync();
        if (user is not null)
        {
            logger.LogInformation("User initialized {Username}", user.DisplayName);
            return user;
        }

        return null;
    }

    public async Task<IReadOnlyList<TwitchRewardSnapshot>> GetRewardsAsync(string accessToken, string channelId)
    {
        if (string.IsNullOrWhiteSpace(channelId))
        {
            logger.LogWarning("Cannot refresh rewards: ChannelId is null or empty");
            return [];
        }

        var api = apiFactory.Create(accessToken);
        try
        {
            var rewards = await api.GetRewardsAsync(channelId);
            if (rewards.Count == 0)
            {
                logger.LogWarning("No channel points rewards found for {ChannelId}", channelId);
            }

            return rewards;
        }
        catch
        {
            // user has no channel points rewards configured or is not affiliated or something
            logger.LogWarning("Failed to get channel points rewards for {ChannelId}", channelId);
            return [];
        }
    }
}
