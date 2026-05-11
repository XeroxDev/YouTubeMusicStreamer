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

using XeroxDev.YTMDesktop.Companion.Models.Output;

namespace YouTubeMusicStreamer.Services.YouTube;

public interface IYtmPlaybackController
{
    bool IsAvailable { get; }
    StateOutput? LastKnownState { get; }
    Task<StateOutput?> GetStateAsync();
    Task ChangeVideoAsync(string videoId);
    Task NextAsync();
    Task PreviousAsync();
    Task SetVolumeAsync(int volume);
}

public sealed class YtmPlaybackController(IYtmCompanionSessionCoordinator sessionCoordinator) : IYtmPlaybackController
{
    public bool IsAvailable => sessionCoordinator.RestClient is not null;

    public StateOutput? LastKnownState => sessionCoordinator.State.PlaybackSnapshot?.State;

    public async Task<StateOutput?> GetStateAsync()
    {
        if (sessionCoordinator.RestClient is null)
            return null;

        return await sessionCoordinator.RestClient.GetStateAsync();
    }

    public async Task ChangeVideoAsync(string videoId)
    {
        if (sessionCoordinator.RestClient is null)
            throw new InvalidOperationException("YTMDesktop is not connected.");

        await sessionCoordinator.RestClient.ChangeVideoAsync(videoId);
    }

    public async Task NextAsync()
    {
        if (sessionCoordinator.RestClient is null)
            throw new InvalidOperationException("YTMDesktop is not connected.");

        await sessionCoordinator.RestClient.NextAsync();
    }

    public async Task PreviousAsync()
    {
        if (sessionCoordinator.RestClient is null)
            throw new InvalidOperationException("YTMDesktop is not connected.");

        await sessionCoordinator.RestClient.PreviousAsync();
    }

    public async Task SetVolumeAsync(int volume)
    {
        if (sessionCoordinator.RestClient is null)
            throw new InvalidOperationException("YTMDesktop is not connected.");

        await sessionCoordinator.RestClient.SetVolumeAsync(volume);
    }
}
