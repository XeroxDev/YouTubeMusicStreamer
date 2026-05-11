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

using Microsoft.Extensions.Logging.Abstractions;
using System.Net.Http;
using XeroxDev.YTMDesktop.Companion.Models.Output;
using QueueEntry = YouTubeMusicStreamer.Models.QueueItem;
using YouTubeMusicStreamer.Models;
using YouTubeMusicStreamer.Services.App;
using YouTubeMusicStreamer.Services.App.Persistence;
using YouTubeMusicStreamer.Services.Commands.Workflows;
using YouTubeMusicStreamer.Services.YouTube;
using YouTubeMusicStreamer.Tests.TestSupport;

namespace YouTubeMusicStreamer.Tests.Services.Commands.Workflows;

public class YouTubeCommandWorkflowTests
{
    [Fact]
    public async Task GetCurrentSongAsync_ReturnsSongInfo_WhenCachedPlaybackStateExists()
    {
        await using var fixture = await CreateFixtureAsync();
        fixture.PlaybackController.LastKnownState = CreateStateOutput("abc123", "Never Gonna Give You Up", "Rick Astley", 212);

        var result = await fixture.Workflow.GetCurrentSongAsync();

        Assert.Equal(YouTubeWorkflowStatus.Success, result.Status);
        Assert.Equal("Never Gonna Give You Up", result.Value!.Title);
        Assert.Equal("Rick Astley", result.Value.Author);
        Assert.Equal("https://youtu.be/abc123", result.Value.Url);
        Assert.Equal("03:32", result.Value.Duration);
    }

    [Fact]
    public async Task GetCurrentSongAsync_ReturnsUnavailable_WhenPlaybackControllerIsUnavailable()
    {
        await using var fixture = await CreateFixtureAsync();
        fixture.PlaybackController.IsAvailable = false;

        var result = await fixture.Workflow.GetCurrentSongAsync();

        Assert.Equal(YouTubeWorkflowStatus.Unavailable, result.Status);
        Assert.Equal("YTMDesktop is not connected.", result.Message);
    }

    [Fact]
    public async Task GetCurrentSongAsync_ReturnsSongInfo_WhenLiveStateIsFetchedWithoutCache()
    {
        await using var fixture = await CreateFixtureAsync();
        fixture.PlaybackController.LiveState = CreateStateOutput("live123", "Live Song", "Live Artist", 95);

        var result = await fixture.Workflow.GetCurrentSongAsync();

        Assert.Equal(YouTubeWorkflowStatus.Success, result.Status);
        Assert.Equal("Live Song", result.Value!.Title);
        Assert.Equal("Live Artist", result.Value.Author);
        Assert.Equal("01:35", result.Value.Duration);
    }

    [Fact]
    public async Task GetCurrentSongAsync_ReturnsBlocked_WhenLiveStateFetchReturnsNull()
    {
        await using var fixture = await CreateFixtureAsync();

        var result = await fixture.Workflow.GetCurrentSongAsync();

        Assert.Equal(YouTubeWorkflowStatus.Blocked, result.Status);
        Assert.Equal("No current song is available.", result.Message);
    }

    [Fact]
    public async Task QueueSongAsync_ReturnsBlocked_WhenVideoIsBlacklisted()
    {
        await using var fixture = await CreateFixtureAsync();
        await fixture.Settings.ReplaceBlacklistEntriesAsync([new BlacklistEntry(new Uri("https://youtu.be/abc123"), "Nope")]);

        var result = await fixture.Workflow.QueueSongAsync("TestUser", "!request https://youtu.be/abc123", "https://youtu.be/abc123");

        Assert.Equal(YouTubeWorkflowStatus.Blocked, result.Status);
        Assert.Equal("That video is blacklisted.", result.Message);
        Assert.Empty(fixture.Settings.GetQueueItems());
        Assert.Null(fixture.MetadataResolver.LastRequestedVideoId);
    }

    [Fact]
    public async Task QueueSongAsync_DoesNotTreatDifferentBlacklistedVideoIdSubstringAsMatch()
    {
        await using var fixture = await CreateFixtureAsync();
        await fixture.Settings.ReplaceBlacklistEntriesAsync([new BlacklistEntry(new Uri("https://youtu.be/abc1234"), "Different video")]);
        fixture.MetadataResolver.Embed = CreateEmbed(title: "Song Title", authorName: "Artist", authorUrl: "Channel");

        var result = await fixture.Workflow.QueueSongAsync("TestUser", "!request https://youtu.be/abc123", "https://youtu.be/abc123");

        Assert.Equal(YouTubeWorkflowStatus.Success, result.Status);
        Assert.Equal("abc123", Assert.Single(fixture.Settings.GetQueueItems()).Id);
    }

    [Fact]
    public async Task QueueSongAsync_PersistsQueueItem_WhenMetadataResolvesAndVideoIsAllowed()
    {
        await using var fixture = await CreateFixtureAsync();
        fixture.MetadataResolver.Embed = CreateEmbed(title: "Song Title", authorName: "Artist", authorUrl: "Channel");

        var result = await fixture.Workflow.QueueSongAsync("TestUser", "!request https://youtu.be/abc123", "https://youtu.be/abc123");

        Assert.Equal(YouTubeWorkflowStatus.Success, result.Status);
        Assert.Equal("Song Title", result.Value!.Title);
        Assert.Equal("Artist", result.Value.Author);
        Assert.Equal("Channel", result.Value.Channel);
        Assert.Equal("https://www.youtube.com/watch?v=abc123", result.Value.Url);

        var queued = Assert.Single(fixture.Settings.GetQueueItems());
        Assert.Equal("abc123", queued.Id);
        Assert.Equal("TestUser", queued.Requester);
        Assert.Equal("!request https://youtu.be/abc123", queued.Message);
        Assert.Equal("abc123", fixture.MetadataResolver.LastRequestedVideoId);
    }

    [Fact]
    public async Task QueueSongAsync_ReturnsInvalidInput_WhenUrlIsNotAValidYouTubeVideo()
    {
        await using var fixture = await CreateFixtureAsync();

        var result = await fixture.Workflow.QueueSongAsync("TestUser", "!request bad-url", "bad-url");

        Assert.Equal(YouTubeWorkflowStatus.InvalidInput, result.Status);
        Assert.Equal("The provided URL is not a valid YouTube video.", result.Message);
        Assert.Null(fixture.MetadataResolver.LastRequestedVideoId);
    }

    [Fact]
    public async Task QueueSongAsync_ReturnsInvalidInput_WhenMetadataCouldNotBeResolved()
    {
        await using var fixture = await CreateFixtureAsync();

        var result = await fixture.Workflow.QueueSongAsync("TestUser", "!request https://youtu.be/abc123", "https://youtu.be/abc123");

        Assert.Equal(YouTubeWorkflowStatus.InvalidInput, result.Status);
        Assert.Equal("Could not resolve video metadata for the requested song.", result.Message);
        Assert.Empty(fixture.Settings.GetQueueItems());
    }

    [Fact]
    public async Task QueueSongAsync_ReturnsUnavailable_WhenMetadataResolverThrows()
    {
        await using var fixture = await CreateFixtureAsync();
        fixture.MetadataResolver.ExceptionToThrow = new HttpRequestException("resolver failed");

        var result = await fixture.Workflow.QueueSongAsync("TestUser", "!request https://youtu.be/abc123", "https://youtu.be/abc123");

        Assert.Equal(YouTubeWorkflowStatus.Unavailable, result.Status);
        Assert.Equal("YTMDesktop is temporarily unavailable. Please try again in a moment.", result.Message);
        Assert.Empty(fixture.Settings.GetQueueItems());
    }

    [Fact]
    public async Task StartSongAsync_StartsPlayback_WhenMetadataResolvesAndVideoIsAllowed()
    {
        await using var fixture = await CreateFixtureAsync();
        fixture.MetadataResolver.Embed = CreateEmbed(title: "Song Title", authorName: "Artist", authorUrl: "Channel");

        var result = await fixture.Workflow.StartSongAsync("https://youtu.be/abc123");

        Assert.Equal(YouTubeWorkflowStatus.Success, result.Status);
        Assert.Equal("abc123", fixture.PlaybackController.LastChangedVideoId);
    }

    [Fact]
    public async Task StartSongAsync_ReturnsInvalidInput_WhenUrlIsNotAValidYouTubeVideo()
    {
        await using var fixture = await CreateFixtureAsync();

        var result = await fixture.Workflow.StartSongAsync("bad-url");

        Assert.Equal(YouTubeWorkflowStatus.InvalidInput, result.Status);
        Assert.Equal("The provided URL is not a valid YouTube video.", result.Message);
    }

    [Fact]
    public async Task StartSongAsync_ReturnsBlocked_WhenVideoIsBlacklisted()
    {
        await using var fixture = await CreateFixtureAsync();
        await fixture.Settings.ReplaceBlacklistEntriesAsync([new BlacklistEntry(new Uri("https://youtu.be/abc123"), "Nope")]);

        var result = await fixture.Workflow.StartSongAsync("https://youtu.be/abc123");

        Assert.Equal(YouTubeWorkflowStatus.Blocked, result.Status);
        Assert.Equal("That video is blacklisted.", result.Message);
        Assert.Null(fixture.PlaybackController.LastChangedVideoId);
    }

    [Fact]
    public async Task StartSongAsync_DoesNotTreatDifferentBlacklistedVideoIdSubstringAsMatch()
    {
        await using var fixture = await CreateFixtureAsync();
        await fixture.Settings.ReplaceBlacklistEntriesAsync([new BlacklistEntry(new Uri("https://youtu.be/abc1234"), "Different video")]);
        fixture.MetadataResolver.Embed = CreateEmbed(title: "Song Title", authorName: "Artist", authorUrl: "Channel");

        var result = await fixture.Workflow.StartSongAsync("https://youtu.be/abc123");

        Assert.Equal(YouTubeWorkflowStatus.Success, result.Status);
        Assert.Equal("abc123", fixture.PlaybackController.LastChangedVideoId);
    }

    [Fact]
    public async Task StartSongAsync_ReturnsUnavailable_WhenPlaybackStartFailsAfterMetadataResolution()
    {
        await using var fixture = await CreateFixtureAsync();
        fixture.MetadataResolver.Embed = CreateEmbed(title: "Song Title", authorName: "Artist", authorUrl: "Channel");
        fixture.PlaybackController.ChangeVideoException = new HttpRequestException("boom");

        var result = await fixture.Workflow.StartSongAsync("https://youtu.be/abc123");

        Assert.Equal(YouTubeWorkflowStatus.Unavailable, result.Status);
        Assert.Equal("YTMDesktop is temporarily unavailable. Please try again in a moment.", result.Message);
    }

    [Fact]
    public async Task StartSongAsync_ReturnsUnavailable_WhenMetadataResolverThrows()
    {
        await using var fixture = await CreateFixtureAsync();
        fixture.MetadataResolver.ExceptionToThrow = new TaskCanceledException("resolver timed out");

        var result = await fixture.Workflow.StartSongAsync("https://youtu.be/abc123");

        Assert.Equal(YouTubeWorkflowStatus.Unavailable, result.Status);
        Assert.Equal("YTMDesktop is temporarily unavailable. Please try again in a moment.", result.Message);
        Assert.Null(fixture.PlaybackController.LastChangedVideoId);
    }

    [Fact]
    public async Task NextAsync_StartsFirstQueuedSongAndRemovesIt_WhenQueueIsActive()
    {
        await using var fixture = await CreateFixtureAsync();
        SettingsServiceTestSupport.SetPrivateField(fixture.Settings, "_queueSettings", new QueueSettingsSnapshot(true));
        await fixture.Settings.ReplaceQueueItemsAsync(
        [
            new QueueEntry("song-1", "TestUser", "!request song-1"),
            new QueueEntry("song-2", "TestUser", "!request song-2")
        ]);

        var result = await fixture.Workflow.NextAsync();

        Assert.Equal(YouTubeWorkflowStatus.Success, result.Status);
        Assert.Equal("song-1", fixture.PlaybackController.LastChangedVideoId);
        var remaining = fixture.Settings.GetQueueItems();
        Assert.Single(remaining);
        Assert.Equal("song-2", remaining[0].Id);
        Assert.Equal(0, fixture.PlaybackController.NextCallCount);
    }

    [Fact]
    public async Task NextAsync_ReturnsUnavailableAndKeepsQueue_WhenQueuedPlaybackStartFails()
    {
        await using var fixture = await CreateFixtureAsync();
        SettingsServiceTestSupport.SetPrivateField(fixture.Settings, "_queueSettings", new QueueSettingsSnapshot(true));
        await fixture.Settings.ReplaceQueueItemsAsync(
        [
            new QueueEntry("song-1", "TestUser", "!request song-1"),
            new QueueEntry("song-2", "TestUser", "!request song-2")
        ]);
        fixture.PlaybackController.ChangeVideoException = new HttpRequestException("boom");

        var result = await fixture.Workflow.NextAsync();

        Assert.Equal(YouTubeWorkflowStatus.Unavailable, result.Status);
        Assert.Equal("YTMDesktop is temporarily unavailable. Please try again in a moment.", result.Message);
        Assert.Equal(2, fixture.Settings.GetQueueItems().Count);
    }

    [Fact]
    public async Task NextAsync_DelegatesToPlaybackController_WhenQueueIsInactive()
    {
        await using var fixture = await CreateFixtureAsync();
        SettingsServiceTestSupport.SetPrivateField(fixture.Settings, "_queueSettings", new QueueSettingsSnapshot(false));

        var result = await fixture.Workflow.NextAsync();

        Assert.Equal(YouTubeWorkflowStatus.Success, result.Status);
        Assert.Equal(1, fixture.PlaybackController.NextCallCount);
        Assert.Null(fixture.PlaybackController.LastChangedVideoId);
    }

    [Fact]
    public async Task SetVolumeAsync_ClampsVolume_WhenRequestedVolumeIsOutsideAllowedRange()
    {
        await using var fixture = await CreateFixtureAsync();

        var result = await fixture.Workflow.SetVolumeAsync(150);

        Assert.Equal(YouTubeWorkflowStatus.Success, result.Status);
        Assert.Equal(100, result.Value);
        Assert.Equal(100, fixture.PlaybackController.LastSetVolume);
    }

    [Fact]
    public async Task SetRandomVolumeAsync_ReturnsGeneratedVolume_WhenPlaybackControllerSucceeds()
    {
        await using var fixture = await CreateFixtureAsync();

        var result = await fixture.Workflow.SetRandomVolumeAsync();

        Assert.Equal(YouTubeWorkflowStatus.Success, result.Status);
        Assert.InRange(result.Value, 0, 100);
        Assert.Equal(result.Value, fixture.PlaybackController.LastSetVolume);
    }

    [Fact]
    public async Task SetRandomVolumeAsync_ReturnsUnavailable_WhenPlaybackControllerFails()
    {
        await using var fixture = await CreateFixtureAsync();
        fixture.PlaybackController.SetVolumeException = new HttpRequestException("boom");

        var result = await fixture.Workflow.SetRandomVolumeAsync();

        Assert.Equal(YouTubeWorkflowStatus.Unavailable, result.Status);
        Assert.Equal("YTMDesktop is temporarily unavailable. Please try again in a moment.", result.Message);
    }

    [Fact]
    public async Task PreviousAsync_ReturnsUnavailable_WhenPlaybackCallThrowsApiException()
    {
        await using var fixture = await CreateFixtureAsync();
        fixture.PlaybackController.PreviousException = new HttpRequestException("boom");

        var result = await fixture.Workflow.PreviousAsync();

        Assert.Equal(YouTubeWorkflowStatus.Unavailable, result.Status);
        Assert.Equal("YTMDesktop is temporarily unavailable. Please try again in a moment.", result.Message);
    }

    private static async Task<WorkflowFixture> CreateFixtureAsync()
    {
        var dbFactory = new InMemorySqliteDbContextFactory();
        var settings = SettingsServiceTestSupport.CreateWithInMemoryDb(
            dbFactory,
            twitchSettings: new TwitchSettingsSnapshot(true, "Connected!", "!", null, null));
        await settings.ReplaceBlacklistEntriesAsync([]);
        await settings.ReplaceQueueItemsAsync([]);

        var playbackController = new FakeYtmPlaybackController();
        var metadataResolver = new FakeYouTubeMetadataResolver();
        var workflow = new YouTubeCommandWorkflow(
            settings,
            playbackController,
            metadataResolver,
            NullLogger<YouTubeCommandWorkflow>.Instance);

        return new WorkflowFixture(settings, workflow, playbackController, metadataResolver, dbFactory);
    }

    private static StateOutput CreateStateOutput(string id, string title, string author, int durationSeconds) =>
        new()
        {
            Video = new Video
            {
                Id = id,
                Title = title,
                Author = author,
                DurationSeconds = durationSeconds
            }
        };

    private static YouTubeEmbed CreateEmbed(string title, string authorName, string authorUrl) =>
        new(
            title,
            authorName,
            authorUrl,
            "video",
            0,
            0,
            "1.0",
            "YouTube",
            "https://www.youtube.com",
            0,
            0,
            string.Empty,
            string.Empty);

    private sealed class WorkflowFixture(
        SettingsService settings,
        YouTubeCommandWorkflow workflow,
        FakeYtmPlaybackController playbackController,
        FakeYouTubeMetadataResolver metadataResolver,
        InMemorySqliteDbContextFactory dbFactory) : IAsyncDisposable
    {
        public SettingsService Settings { get; } = settings;
        public YouTubeCommandWorkflow Workflow { get; } = workflow;
        public FakeYtmPlaybackController PlaybackController { get; } = playbackController;
        public FakeYouTubeMetadataResolver MetadataResolver { get; } = metadataResolver;

        public ValueTask DisposeAsync() => dbFactory.DisposeAsync();
    }

    private sealed class FakeYtmPlaybackController : IYtmPlaybackController
    {
        public bool IsAvailable { get; set; } = true;
        public StateOutput? LastKnownState { get; set; }
        public StateOutput? LiveState { get; set; }
        public string? LastChangedVideoId { get; private set; }
        public int? LastSetVolume { get; private set; }
        public int NextCallCount { get; private set; }
        public Exception? PreviousException { get; set; }
        public Exception? ChangeVideoException { get; set; }
        public Exception? SetVolumeException { get; set; }

        public Task<StateOutput?> GetStateAsync() => Task.FromResult(LiveState);

        public Task ChangeVideoAsync(string videoId)
        {
            if (ChangeVideoException is not null)
                return Task.FromException(ChangeVideoException);

            LastChangedVideoId = videoId;
            return Task.CompletedTask;
        }

        public Task NextAsync()
        {
            NextCallCount++;
            return Task.CompletedTask;
        }

        public Task PreviousAsync()
        {
            if (PreviousException is not null)
                return Task.FromException(PreviousException);

            return Task.CompletedTask;
        }

        public Task SetVolumeAsync(int volume)
        {
            if (SetVolumeException is not null)
                return Task.FromException(SetVolumeException);

            LastSetVolume = volume;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeYouTubeMetadataResolver : IYouTubeMetadataResolver
    {
        public string? LastRequestedVideoId { get; private set; }
        public YouTubeEmbed? Embed { get; set; }
        public Exception? ExceptionToThrow { get; set; }

        public Task<YouTubeEmbed?> GetEmbedAsync(string videoId, CancellationToken cancellationToken = default)
        {
            LastRequestedVideoId = videoId;
            if (ExceptionToThrow is not null)
                return Task.FromException<YouTubeEmbed?>(ExceptionToThrow);

            return Task.FromResult(Embed);
        }
    }
}
