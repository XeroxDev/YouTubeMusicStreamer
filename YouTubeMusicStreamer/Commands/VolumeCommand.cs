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

[Command("Changes the volume", false, 300)]
public class VolumeCommand(CommandBag bag) : CommandBase(bag)
{
    [UsedImplicitly]
    protected async Task<CommandExecutionResult<object?>> ExecuteCommandLogicAsync(
        [Placeholder("The volume the user provided")]
        int volume
    )
    {
        if (Bag.YouTubeService.RestClient is null)
        {
            return new CommandExecutionResult<object?>(false);
        }

        if (volume < 0) volume = 0;
        if (volume > 100) volume = 100;

        await Bag.YouTubeService.RestClient.SetVolume(volume);

        return new CommandExecutionResult<object?>(true);
    }
}