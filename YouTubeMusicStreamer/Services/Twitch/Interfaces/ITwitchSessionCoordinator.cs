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

namespace YouTubeMusicStreamer.Services.Twitch.Interfaces;

public interface ITwitchSessionCoordinator
{
    TwitchSessionState CurrentState { get; }
    event EventHandler<TwitchSessionState> StateChanged;
    event EventHandler<TwitchSessionFailure> SessionFaulted;

    Task InitializeIfNeededAsync();
    Task<TwitchSessionFailure?> StartBroadcasterAuthAsync(CancellationToken cancellationToken = default);
    Task<TwitchSessionFailure?> StartBotAuthAsync(CancellationToken cancellationToken = default);
    Task DisconnectBotAsync();
    Task LogoutAsync();
    Task RefreshRewardsAsync();
    Task<ManagedRewardSyncResult> RefreshManagedRewardBindingAsync(CommandRewardBindingSnapshot binding, bool commandRequiresInput, CancellationToken cancellationToken = default);
    Task<ManagedRewardSyncResult> CreateManagedRewardAsync(CommandRewardBindingSnapshot binding, bool commandRequiresInput, CancellationToken cancellationToken = default);
    Task<ManagedRewardSyncResult> UpdateManagedRewardAsync(CommandRewardBindingSnapshot binding, bool commandRequiresInput, CancellationToken cancellationToken = default);
}
