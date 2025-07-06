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

[Command("Send information about the current song.", true, 60, "The currently playing song is {title} by {author}. It is {duration} long. You can listen to it here: {url}")]
public class InfoCommand(CommandBag bag) : CommandBase(bag)
{
    [UsedImplicitly]
    protected async Task<CommandExecutionResult<InfoResult>> ExecuteCommandLogicAsync()
    {
        if (Bag.YouTubeService.RestClient is null)
        {
            return new CommandExecutionResult<InfoResult>(false);
        }

        var currentSong = await Bag.YouTubeService.RestClient.GetState();

        if (currentSong is null)
        {
            return new CommandExecutionResult<InfoResult>(false);
        }

        var ts = TimeSpan.FromSeconds(currentSong.Video.DurationSeconds);

        return new CommandExecutionResult<InfoResult>(true, new InfoResult()
        {
            Author = currentSong.Video.Author,
            Title = currentSong.Video.Title,
            Duration = ts.TotalHours >= 1 ? ts.ToString(@"hh\:mm\:ss") : ts.ToString(@"mm\:ss"),
            Url = $"https://youtu.be/{currentSong.Video.Id}"
        });
    }

    public class InfoResult
    {
        [Placeholder("The title of the currently playing song.")]
        public string Title { [UsedImplicitly] get; init; } = "";

        [Placeholder("The author of the currently playing song.")]
        public string Author { [UsedImplicitly] get; init; } = "";

        [Placeholder("The duration of the currently playing song.")]
        public string Duration { [UsedImplicitly] get; init; } = "";

        [Placeholder("A link to listen to the current song.")]
        public string Url { [UsedImplicitly] get; init; } = "";
    }
}