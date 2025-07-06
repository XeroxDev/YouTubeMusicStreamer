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
using XeroxDev.YTMDesktop.Companion.Models.Output;
using YouTubeMusicStreamer.Services.App;

namespace YouTubeMusicStreamer.Services.YouTube;

public class SongQueueService(SettingsService settingsService, ILogger<SongQueueService> logger)
{
    private TimeSpan _currentSongDuration;
    private bool _isSwitchingSong;

    public async Task YouTubeStateChanged(YouTubeService youtubeService, StateOutput e)
    {
        try
        {
            if (settingsService.GetAppSettings().Queue.Count == 0 || !settingsService.GetAppSettings().QueueActive) return;

            if (_isSwitchingSong)
            {
                return;
            }

            _currentSongDuration = TimeSpan.FromSeconds(e.Video.DurationSeconds);
            var currentTime = TimeSpan.FromSeconds(e.Player.VideoProgress);

            if (_currentSongDuration - currentTime > TimeSpan.FromSeconds(3)) return;

            var nextSong = settingsService.GetAppSettings().Queue.FirstOrDefault();
            if (nextSong is null) return;
            _isSwitchingSong = true;
            await youtubeService.RestClient!.ChangeVideo(nextSong.Id);
            await settingsService.SaveAppSettingAsync(settings => { settings.Queue.RemoveAt(0); });
            
            // Wait 3 seconds before allowing the next song to be switched
            await Task.Delay(3000);
            _isSwitchingSong = false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while switching songs");
        }
    }
}