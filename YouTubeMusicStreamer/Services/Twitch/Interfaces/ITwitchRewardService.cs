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

namespace YouTubeMusicStreamer.Services.Twitch.Interfaces;

public interface ITwitchRewardService
{
    Task<TwitchManagedReward?> GetRewardByIdAsync(string accessToken, string broadcasterId, string rewardId, CancellationToken cancellationToken = default);
    Task<TwitchManagedReward> CreateManagedRewardAsync(string accessToken, string broadcasterId, TwitchManagedRewardDefinition definition, CancellationToken cancellationToken = default);
    Task<TwitchManagedReward> UpdateManagedRewardAsync(string accessToken, string broadcasterId, string rewardId, TwitchManagedRewardDefinition definition, CancellationToken cancellationToken = default);
    Task UpdateRedemptionStatusAsync(string accessToken, string broadcasterId, string rewardId, string redemptionId, TwitchRedemptionStatus status, CancellationToken cancellationToken = default);
}
