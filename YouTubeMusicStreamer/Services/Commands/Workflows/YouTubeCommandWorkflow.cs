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

using Microsoft.Extensions.Logging;
using XeroxDev.YTMDesktop.Companion.Exceptions;
using YouTubeMusicStreamer.Models;
using YouTubeMusicStreamer.Services.App;
using YouTubeMusicStreamer.Services.YouTube;

namespace YouTubeMusicStreamer.Services.Commands.Workflows;

public sealed class YouTubeCommandWorkflow(
    SettingsService settingsService,
    IYtmPlaybackController playbackController,
    IYouTubeMetadataResolver metadataResolver,
    ILogger<YouTubeCommandWorkflow> logger) : IYouTubeCommandWorkflow
{
    public async Task<YouTubeWorkflowResult<YouTubeSongInfo>> GetCurrentSongAsync()
    {
        var cachedState = playbackController.LastKnownState;
        if (cachedState is not null)
            return CreateCurrentSongResult(cachedState);

        if (!playbackController.IsAvailable)
            return new YouTubeWorkflowResult<YouTubeSongInfo>(YouTubeWorkflowStatus.Unavailable, Message: "YTMDesktop is not connected.");

        var currentSongResult = await TryCallWithStatusAsync(
            () => playbackController.GetStateAsync(),
            "Failed to query the current YTMDesktop song state.");
        if (!currentSongResult.Success)
            return new YouTubeWorkflowResult<YouTubeSongInfo>(YouTubeWorkflowStatus.Unavailable, Message: "YTMDesktop is temporarily unavailable. Please try again in a moment.");

        var currentSong = currentSongResult.Value;
        if (currentSong is null)
            return new YouTubeWorkflowResult<YouTubeSongInfo>(YouTubeWorkflowStatus.Blocked, Message: "No current song is available.");

        return CreateCurrentSongResult(currentSong);
    }

    public async Task<YouTubeWorkflowResult<YouTubeSongInfo>> QueueSongAsync(string requestedBy, string sourceMessage, string url)
    {
        if (!playbackController.IsAvailable)
            return new YouTubeWorkflowResult<YouTubeSongInfo>(YouTubeWorkflowStatus.Unavailable, Message: "YTMDesktop is not connected.");

        var videoId = YouTubeService.GetVideoId(url);
        if (videoId is null)
            return new YouTubeWorkflowResult<YouTubeSongInfo>(YouTubeWorkflowStatus.InvalidInput, Message: "The provided URL is not a valid YouTube video.");

        if (IsBlacklistedVideo(videoId))
            return new YouTubeWorkflowResult<YouTubeSongInfo>(YouTubeWorkflowStatus.Blocked, Message: "That video is blacklisted.");

        var embedResult = await TryResolveEmbedAsync(videoId, "Failed to resolve metadata for the requested queued song.");
        if (!embedResult.Success)
            return new YouTubeWorkflowResult<YouTubeSongInfo>(YouTubeWorkflowStatus.Unavailable, Message: "YTMDesktop is temporarily unavailable. Please try again in a moment.");

        var embed = embedResult.Value;
        if (embed is null)
            return new YouTubeWorkflowResult<YouTubeSongInfo>(YouTubeWorkflowStatus.InvalidInput, Message: "Could not resolve video metadata for the requested song.");

        var song = CreateSongInfo(videoId, embed);
        var item = new QueueItem(videoId, requestedBy, sourceMessage, embed);

        await settingsService.UpdateQueueItemsAsync(items => items.Add(item));
        return new YouTubeWorkflowResult<YouTubeSongInfo>(YouTubeWorkflowStatus.Success, song);
    }

    public async Task<YouTubeWorkflowResult<YouTubeSongInfo>> StartSongAsync(string url)
    {
        if (!playbackController.IsAvailable)
            return new YouTubeWorkflowResult<YouTubeSongInfo>(YouTubeWorkflowStatus.Unavailable, Message: "YTMDesktop is not connected.");

        var videoId = YouTubeService.GetVideoId(url);
        if (videoId is null)
            return new YouTubeWorkflowResult<YouTubeSongInfo>(YouTubeWorkflowStatus.InvalidInput, Message: "The provided URL is not a valid YouTube video.");

        if (IsBlacklistedVideo(videoId))
            return new YouTubeWorkflowResult<YouTubeSongInfo>(YouTubeWorkflowStatus.Blocked, Message: "That video is blacklisted.");

        var embedResult = await TryResolveEmbedAsync(videoId, "Failed to resolve metadata for the requested song.");
        if (!embedResult.Success)
            return new YouTubeWorkflowResult<YouTubeSongInfo>(YouTubeWorkflowStatus.Unavailable, Message: "YTMDesktop is temporarily unavailable. Please try again in a moment.");

        var embed = embedResult.Value;
        if (embed is null)
            return new YouTubeWorkflowResult<YouTubeSongInfo>(YouTubeWorkflowStatus.InvalidInput, Message: "Could not resolve video metadata for the requested song.");

        var song = CreateSongInfo(videoId, embed);
        var changed = await TryCallAsync(
            async () =>
            {
                await playbackController.ChangeVideoAsync(videoId);
                return true;
            },
            "Failed to start the requested song in YTMDesktop.");
        if (changed != true)
            return new YouTubeWorkflowResult<YouTubeSongInfo>(YouTubeWorkflowStatus.Unavailable, Message: "YTMDesktop is temporarily unavailable. Please try again in a moment.");

        return new YouTubeWorkflowResult<YouTubeSongInfo>(YouTubeWorkflowStatus.Success, song);
    }

    public async Task<YouTubeWorkflowResult<bool>> NextAsync()
    {
        if (!playbackController.IsAvailable)
            return new YouTubeWorkflowResult<bool>(YouTubeWorkflowStatus.Unavailable, Message: "YTMDesktop is not connected.");

        var queueSettings = settingsService.GetQueueSettings();
        var queueItems = settingsService.GetQueueItems();
        if (queueSettings.QueueActive && queueItems.Count > 0)
        {
            var nextSong = queueItems.First();
            var changed = await TryCallAsync(
                async () =>
                {
                    await playbackController.ChangeVideoAsync(nextSong.Id);
                    return true;
                },
                "Failed to start the next queued YTMDesktop song.");
            if (changed != true)
                return new YouTubeWorkflowResult<bool>(YouTubeWorkflowStatus.Unavailable, Message: "YTMDesktop is temporarily unavailable. Please try again in a moment.");

            await settingsService.UpdateQueueItemsAsync(items =>
            {
                if (items.Count > 0)
                    items.RemoveAt(0);
            });

            return new YouTubeWorkflowResult<bool>(YouTubeWorkflowStatus.Success, true);
        }

        var advanced = await TryCallAsync(
            async () =>
            {
                await playbackController.NextAsync();
                return true;
            },
            "Failed to skip to the next YTMDesktop track.");
        if (advanced != true)
            return new YouTubeWorkflowResult<bool>(YouTubeWorkflowStatus.Unavailable, Message: "YTMDesktop is temporarily unavailable. Please try again in a moment.");

        return new YouTubeWorkflowResult<bool>(YouTubeWorkflowStatus.Success, true);
    }

    public async Task<YouTubeWorkflowResult<bool>> PreviousAsync()
    {
        if (!playbackController.IsAvailable)
            return new YouTubeWorkflowResult<bool>(YouTubeWorkflowStatus.Unavailable, Message: "YTMDesktop is not connected.");

        var rewound = await TryCallAsync(
            async () =>
            {
                await playbackController.PreviousAsync();
                return true;
            },
            "Failed to skip to the previous YTMDesktop track.");
        if (rewound != true)
            return new YouTubeWorkflowResult<bool>(YouTubeWorkflowStatus.Unavailable, Message: "YTMDesktop is temporarily unavailable. Please try again in a moment.");

        return new YouTubeWorkflowResult<bool>(YouTubeWorkflowStatus.Success, true);
    }

    public async Task<YouTubeWorkflowResult<int>> SetRandomVolumeAsync()
    {
        if (!playbackController.IsAvailable)
            return new YouTubeWorkflowResult<int>(YouTubeWorkflowStatus.Unavailable, Message: "YTMDesktop is not connected.");

        var volume = Random.Shared.Next(0, 101);
        var changed = await TryCallAsync(
            async () =>
            {
                await playbackController.SetVolumeAsync(volume);
                return true;
            },
            "Failed to set a random YTMDesktop volume.");
        if (changed != true)
            return new YouTubeWorkflowResult<int>(YouTubeWorkflowStatus.Unavailable, Message: "YTMDesktop is temporarily unavailable. Please try again in a moment.");

        return new YouTubeWorkflowResult<int>(YouTubeWorkflowStatus.Success, volume);
    }

    public async Task<YouTubeWorkflowResult<int>> SetVolumeAsync(int volume)
    {
        if (!playbackController.IsAvailable)
            return new YouTubeWorkflowResult<int>(YouTubeWorkflowStatus.Unavailable, Message: "YTMDesktop is not connected.");

        var clampedVolume = Math.Clamp(volume, 0, 100);
        var changed = await TryCallAsync(
            async () =>
            {
                await playbackController.SetVolumeAsync(clampedVolume);
                return true;
            },
            "Failed to set the YTMDesktop volume.");
        if (changed != true)
            return new YouTubeWorkflowResult<int>(YouTubeWorkflowStatus.Unavailable, Message: "YTMDesktop is temporarily unavailable. Please try again in a moment.");

        return new YouTubeWorkflowResult<int>(YouTubeWorkflowStatus.Success, clampedVolume);
    }

    private async Task<T?> TryCallAsync<T>(Func<Task<T>> action, string failureMessage)
    {
        try
        {
            return await action();
        }
        catch (ApiException ex)
        {
            logger.LogWarning(ex, failureMessage);
            return default;
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, failureMessage);
            return default;
        }
        catch (TaskCanceledException ex)
        {
            logger.LogWarning(ex, failureMessage);
            return default;
        }
    }

    private async Task<(bool Success, T? Value)> TryCallWithStatusAsync<T>(Func<Task<T>> action, string failureMessage)
    {
        try
        {
            return (true, await action());
        }
        catch (ApiException ex)
        {
            logger.LogWarning(ex, failureMessage);
            return (false, default);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, failureMessage);
            return (false, default);
        }
        catch (TaskCanceledException ex)
        {
            logger.LogWarning(ex, failureMessage);
            return (false, default);
        }
    }

    private async Task<(bool Success, YouTubeEmbed? Value)> TryResolveEmbedAsync(string videoId, string failureMessage)
    {
        try
        {
            return (true, await metadataResolver.GetEmbedAsync(videoId));
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, failureMessage);
            return (false, default);
        }
        catch (TaskCanceledException ex)
        {
            logger.LogWarning(ex, failureMessage);
            return (false, default);
        }
    }

    private bool IsBlacklistedVideo(string videoId) =>
        settingsService.GetBlacklistEntries()
            .Select(entry => YouTubeService.GetVideoId(entry.Url.ToString()))
            .Any(blacklistedVideoId => string.Equals(blacklistedVideoId, videoId, StringComparison.OrdinalIgnoreCase));

    private static YouTubeSongInfo CreateSongInfo(string videoId, YouTubeEmbed embed) =>
        new(
            embed.Title ?? string.Empty,
            embed.AuthorName ?? string.Empty,
            embed.AuthorUrl ?? string.Empty,
            $"https://www.youtube.com/watch?v={videoId}");

    private static YouTubeWorkflowResult<YouTubeSongInfo> CreateCurrentSongResult(XeroxDev.YTMDesktop.Companion.Models.Output.StateOutput currentSong)
    {
        var duration = TimeSpan.FromSeconds(currentSong.Video.DurationSeconds);
        var info = new YouTubeSongInfo(
            currentSong.Video.Title,
            currentSong.Video.Author,
            string.Empty,
            $"https://youtu.be/{currentSong.Video.Id}",
            duration.TotalHours >= 1 ? duration.ToString(@"hh\:mm\:ss") : duration.ToString(@"mm\:ss"));

        return new YouTubeWorkflowResult<YouTubeSongInfo>(YouTubeWorkflowStatus.Success, info);
    }
}
