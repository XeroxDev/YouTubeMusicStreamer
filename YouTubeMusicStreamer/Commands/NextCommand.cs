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

using JetBrains.Annotations;
using YouTubeMusicStreamer.Attributes;
using YouTubeMusicStreamer.Models;
using YouTubeMusicStreamer.Services.Commands;

namespace YouTubeMusicStreamer.Commands;

[Command("Triggers the next command in YTMDesktop", false, 300)]
public class NextCommand(CommandBag bag) : CommandBase(bag)
{
    [UsedImplicitly]
    protected async Task<CommandExecutionResult<object?>> ExecuteCommandLogicAsync()
    {
        if (Bag.YouTubeService.RestClient is null)
        {
            return new CommandExecutionResult<object?>(false);
        }

        var settings = Bag.SettingsService.GetAppSettings();
        if (settings is { QueueActive: true, Queue.Count: > 0 })
        {
            var nextSong = Bag.SettingsService.GetAppSettings().Queue.FirstOrDefault();
            if (nextSong is not null)
            {
                await Bag.YouTubeService.RestClient!.ChangeVideo(nextSong.Id);
                await Bag.SettingsService.SaveAppSettingAsync(s => { s.Queue.RemoveAt(0); });
                return (new CommandExecutionResult<object?>(true));
            }
        }

        await Bag.YouTubeService.RestClient.Next();

        return new CommandExecutionResult<object?>(true);
    }
}