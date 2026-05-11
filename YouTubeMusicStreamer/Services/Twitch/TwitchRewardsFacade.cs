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

using System.ComponentModel;

namespace YouTubeMusicStreamer.Services.Twitch;

public sealed class TwitchRewardsFacade(TwitchService twitchService)
{
    private bool _initialized;

    public event EventHandler? StateChanged;

    public IReadOnlyList<TwitchRewardSnapshot> Rewards { get; private set; } = [];
    public bool IsRefreshing { get; private set; }

    public void Initialize()
    {
        if (_initialized)
            return;

        _initialized = true;
        Rewards = twitchService.Rewards;
        twitchService.PropertyChanged += HandleTwitchPropertyChanged;
    }

    public async Task RefreshAsync()
    {
        if (IsRefreshing)
            return;

        IsRefreshing = true;
        StateChanged?.Invoke(this, EventArgs.Empty);

        try
        {
            await twitchService.RefreshRewardsAsync();
        }
        finally
        {
            Rewards = twitchService.Rewards;
            IsRefreshing = false;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose()
    {
        if (!_initialized)
            return;

        twitchService.PropertyChanged -= HandleTwitchPropertyChanged;
        _initialized = false;
        GC.SuppressFinalize(this);
    }

    private void HandleTwitchPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(twitchService.Rewards))
            return;

        Rewards = twitchService.Rewards;
        IsRefreshing = false;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
