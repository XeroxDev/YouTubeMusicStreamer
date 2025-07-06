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

using System.Net.Http.Json;
using JetBrains.Annotations;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;
using YouTubeMusicStreamer.Attributes;
using YouTubeMusicStreamer.Models;
using YouTubeMusicStreamer.Services.Commands;
using YouTubeService = YouTubeMusicStreamer.Services.YouTube.YouTubeService;

namespace YouTubeMusicStreamer.Commands;

[Command("Adds a song to the queue.", defaultResponse: "The song {title} by {author} has been added to the queue.")]
public class RequestCommand(CommandBag bag) : CommandBase(bag)
{
    [UsedImplicitly]
    protected async Task<CommandExecutionResult<RequestResult>> ExecuteCommandLogicAsync(
        ChannelChatMessage fullMessage,
        [Placeholder("The URL the user provided")]
        string url
    )
    {
        if (Bag.YouTubeService.RestClient is null)
        {
            return new CommandExecutionResult<RequestResult>(false);
        }

        var videoId = YouTubeService.GetVideoId(url);
        if (videoId is null)
        {
            return new CommandExecutionResult<RequestResult>(false);
        }

        var blacklist = Bag.SettingsService.GetAppSettings().Blacklist;

        if (blacklist.Any(b => b.Url.ToString().Contains(videoId)))
        {
            return new CommandExecutionResult<RequestResult>(false);
        }

        using var httpClient = new HttpClient();

        var response = await httpClient.GetAsync($"https://www.youtube.com/oembed?url=https://www.youtube.com/watch?v={videoId}");
        if (!response.IsSuccessStatusCode)
        {
            return new CommandExecutionResult<RequestResult>(false);
        }

        var embed = await response.Content.ReadFromJsonAsync<YouTubeEmbed>();

        var item = new QueueItem(videoId, fullMessage.ChatterUserName, fullMessage.Message.Text, embed);

        await Bag.SettingsService.SaveAppSettingAsync(s => s.Queue.Add(item));

        return new CommandExecutionResult<RequestResult>(true, new RequestResult
        {
            Title = embed?.Title ?? string.Empty,
            Author = embed?.AuthorName ?? string.Empty,
            Channel = embed?.AuthorUrl ?? string.Empty,
            Url = embed?.Html ?? string.Empty
        });
    }

    public class RequestResult
    {
        [Placeholder("The title of the requested song.")]
        public string Title { [UsedImplicitly] get; init; } = "";

        [Placeholder("The author of the requested song.")]
        public string Author { [UsedImplicitly] get; init; } = "";

        [Placeholder("The channel of the requested song.")]
        public string Channel { [UsedImplicitly] get; init; } = "";

        [Placeholder("A link to the requested song.")]
        public string Url { [UsedImplicitly] get; init; } = "";
    }
}