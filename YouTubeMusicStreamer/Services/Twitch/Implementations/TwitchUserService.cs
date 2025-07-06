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
using TwitchLib.Api;
using TwitchLib.Api.Helix.Models.ChannelPoints;
using TwitchLib.Api.Helix.Models.ChannelPoints.GetCustomReward;
using YouTubeMusicStreamer.Services.Twitch.Implementations.EventArgs;
using YouTubeMusicStreamer.Services.Twitch.Interfaces;

namespace YouTubeMusicStreamer.Services.Twitch.Implementations;

public sealed class TwitchUserService(ILogger<TwitchUserService> logger, TwitchAPI api) : ITwitchUserService
{
    public string? Username { get; private set; }
    public string? ChannelId { get; private set; }
    public string? ProfileImageUrl { get; private set; }
    private List<CustomReward> _rewards = [];
    public IReadOnlyList<CustomReward> Rewards => _rewards;

    public event EventHandler<UserInitializedEventArgs>? UserInitialized;

    public async Task InitializeAsync()
    {
        var response = await api.Helix.Users.GetUsersAsync();
        var user = response?.Users.FirstOrDefault();
        if (user != null)
        {
            Username = user.DisplayName;
            ChannelId = user.Id;
            ProfileImageUrl = user.ProfileImageUrl;
            logger.LogInformation("User initialized {Username}", Username);
            UserInitialized?.Invoke(this, new UserInitializedEventArgs(Username, ChannelId, ProfileImageUrl));
        }
    }

    public async Task RefreshRewardsAsync()
    {
        if (string.IsNullOrWhiteSpace(ChannelId))
        {
            logger.LogWarning("Cannot refresh rewards: ChannelId is null or empty");
            _rewards = [];
            return;
        }

        GetCustomRewardsResponse? resp;

        try
        {
            resp = await api.Helix.ChannelPoints.GetCustomRewardAsync(ChannelId);
        }
        catch
        {
            // user has no channel points rewards configured or is not affiliated or something
            logger.LogWarning("Failed to get channel points rewards for {ChannelId}", ChannelId);
            _rewards = [];
            return;
        }

        if (resp?.Data == null || resp.Data.Length == 0)
        {
            logger.LogWarning("No channel points rewards found for {ChannelId}", ChannelId);
            _rewards = [];
        }
        else
        {
            _rewards = resp.Data.OrderBy(r => r.Title, StringComparer.OrdinalIgnoreCase).ToList();
        }
    }
}