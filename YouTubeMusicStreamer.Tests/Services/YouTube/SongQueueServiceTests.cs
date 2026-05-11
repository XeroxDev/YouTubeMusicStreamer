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
using XeroxDev.YTMDesktop.Companion.Models.Output;
using YouTubeMusicStreamer.Services.App;
using YouTubeMusicStreamer.Services.App.Persistence;
using YouTubeMusicStreamer.Services.YouTube;
using YouTubeMusicStreamer.Tests.TestSupport;
using AppQueueItem = YouTubeMusicStreamer.Models.QueueItem;

namespace YouTubeMusicStreamer.Tests.Services.YouTube;

public sealed class SongQueueServiceTests
{
    [Fact]
    public async Task YouTubeStateChanged_StartsNextQueuedSong_WhenTrackEntersEndWindow()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        await harness.Settings.InitializeAsync();
        await harness.Settings.SaveQueueSettingsAsync(new QueueSettingsSnapshot(true));
        await harness.Settings.ReplaceQueueItemsAsync(
        [
            new AppQueueItem("next-video", "Requester", "!request", null)
        ]);

        var playback = new FakePlaybackController();
        var service = CreateService(harness.Settings, playback, harness.Diagnostics);

        await service.YouTubeStateChanged(CreateStateOutput("current-video", durationSeconds: 200, videoProgress: 198.5));

        Assert.Equal(["next-video"], playback.ChangeVideoCalls);
        var queueItems = harness.Settings.GetQueueItems();
        Assert.Single(queueItems);
        Assert.Equal("next-video", queueItems[0].Id);
    }

    [Fact]
    public async Task YouTubeStateChanged_DoesNotStartNextSong_TwiceForSameTrackEndWindow()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        await harness.Settings.InitializeAsync();
        await harness.Settings.SaveQueueSettingsAsync(new QueueSettingsSnapshot(true));
        await harness.Settings.ReplaceQueueItemsAsync(
        [
            new AppQueueItem("next-video", "Requester", "!request", null)
        ]);

        var playback = new FakePlaybackController();
        var service = CreateService(harness.Settings, playback, harness.Diagnostics);
        var state = CreateStateOutput("current-video", durationSeconds: 200, videoProgress: 198.5);

        await service.YouTubeStateChanged(state);
        SettingsServiceTestSupport.SetPrivateField(service, "_pendingTargetVideoId", null as string);
        SettingsServiceTestSupport.SetPrivateField(service, "_pendingTargetStartedAtUtc", null as DateTimeOffset?);

        await service.YouTubeStateChanged(state);

        Assert.Single(playback.ChangeVideoCalls);
    }

    [Fact]
    public async Task YouTubeStateChanged_RemovesConfirmedQueuedItem_WhenPendingTargetStartsPlaying()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        await harness.Settings.InitializeAsync();
        await harness.Settings.SaveQueueSettingsAsync(new QueueSettingsSnapshot(true));
        await harness.Settings.ReplaceQueueItemsAsync(
        [
            new AppQueueItem("next-video", "Requester", "!request", null),
            new AppQueueItem("later-video", "Requester", "!request", null)
        ]);

        var playback = new FakePlaybackController();
        var service = CreateService(harness.Settings, playback, harness.Diagnostics);

        await service.YouTubeStateChanged(CreateStateOutput("current-video", durationSeconds: 200, videoProgress: 198.5));
        await service.YouTubeStateChanged(CreateStateOutput("next-video", durationSeconds: 200, videoProgress: 10));

        var queueItems = harness.Settings.GetQueueItems();
        Assert.Single(queueItems);
        Assert.Equal("later-video", queueItems[0].Id);
    }

    [Fact]
    public async Task YouTubeStateChanged_DoesNotRemoveQueueItem_WhenDifferentSongStartsDuringPendingSwitch()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        await harness.Settings.InitializeAsync();
        await harness.Settings.SaveQueueSettingsAsync(new QueueSettingsSnapshot(true));
        await harness.Settings.ReplaceQueueItemsAsync(
        [
            new AppQueueItem("next-video", "Requester", "!request", null),
            new AppQueueItem("later-video", "Requester", "!request", null)
        ]);

        var playback = new FakePlaybackController();
        var service = CreateService(harness.Settings, playback, harness.Diagnostics);

        await service.YouTubeStateChanged(CreateStateOutput("current-video", durationSeconds: 200, videoProgress: 198.5));
        await service.YouTubeStateChanged(CreateStateOutput("unexpected-video", durationSeconds: 200, videoProgress: 5));

        var queueItems = harness.Settings.GetQueueItems();
        Assert.Equal(["next-video", "later-video"], queueItems.Select(item => item.Id));
        Assert.Equal("next-video", GetPrivateField<string?>(service, "_pendingTargetVideoId"));
        Assert.Empty(harness.Diagnostics.RecentDiagnostics);
    }

    [Fact]
    public async Task YouTubeStateChanged_LogsWarningAndRetries_WhenPendingSwitchTimesOut()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        await harness.Settings.InitializeAsync();
        await harness.Settings.SaveQueueSettingsAsync(new QueueSettingsSnapshot(true));
        await harness.Settings.ReplaceQueueItemsAsync(
        [
            new AppQueueItem("next-video", "Requester", "!request", null)
        ]);

        var playback = new FakePlaybackController();
        var service = CreateService(harness.Settings, playback, harness.Diagnostics);

        await service.YouTubeStateChanged(CreateStateOutput("current-video", durationSeconds: 200, videoProgress: 198.5));
        SettingsServiceTestSupport.SetPrivateField(service, "_pendingTargetStartedAtUtc", DateTimeOffset.UtcNow.AddSeconds(-11));

        await service.YouTubeStateChanged(CreateStateOutput("different-video", durationSeconds: 200, videoProgress: 50));
        await service.YouTubeStateChanged(CreateStateOutput("different-video", durationSeconds: 200, videoProgress: 198.5));

        Assert.Equal(2, playback.ChangeVideoCalls.Count);
        Assert.Contains(
            harness.Diagnostics.RecentDiagnostics,
            diagnostic => diagnostic.Summary == "Timed out waiting for YTMDesktop to confirm queue switch.");
    }

    [Fact]
    public async Task YouTubeStateChanged_DoesNothing_WhenQueueInactive()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        await harness.Settings.InitializeAsync();
        await harness.Settings.SaveQueueSettingsAsync(new QueueSettingsSnapshot(false));
        await harness.Settings.ReplaceQueueItemsAsync(
        [
            new AppQueueItem("next-video", "Requester", "!request", null)
        ]);

        var playback = new FakePlaybackController();
        var service = CreateService(harness.Settings, playback, harness.Diagnostics);

        await service.YouTubeStateChanged(CreateStateOutput("current-video", durationSeconds: 200, videoProgress: 198.5));

        Assert.Empty(playback.ChangeVideoCalls);
    }

    [Fact]
    public async Task YouTubeStateChanged_LogsErrorAndResetsPending_WhenPlaybackSwitchFails()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        await harness.Settings.InitializeAsync();
        await harness.Settings.SaveQueueSettingsAsync(new QueueSettingsSnapshot(true));
        await harness.Settings.ReplaceQueueItemsAsync(
        [
            new AppQueueItem("next-video", "Requester", "!request", null)
        ]);

        var playback = new FakePlaybackController
        {
            ChangeVideoException = new InvalidOperationException("switch failed")
        };
        var service = CreateService(harness.Settings, playback, harness.Diagnostics);

        await service.YouTubeStateChanged(CreateStateOutput("current-video", durationSeconds: 200, videoProgress: 198.5));

        Assert.Contains(
            harness.Diagnostics.RecentDiagnostics,
            diagnostic => diagnostic.Summary == "An error occurred while switching queued songs.");
        Assert.Null(GetPrivateField<string?>(service, "_pendingTargetVideoId"));
        Assert.Null(GetPrivateField<DateTimeOffset?>(service, "_pendingTargetStartedAtUtc"));
        Assert.Single(harness.Settings.GetQueueItems());
    }

    private static SongQueueService CreateService(
        SettingsService settings,
        FakePlaybackController playback,
        RecordingDiagnosticsService diagnostics) =>
        new(settings, playback, NullLogger<SongQueueService>.Instance, diagnostics);

    private static StateOutput CreateStateOutput(string videoId, int durationSeconds, double videoProgress) =>
        new()
        {
            Video = new Video
            {
                Id = videoId,
                Title = "Title",
                Author = "Author",
                ChannelId = "channel",
                DurationSeconds = durationSeconds,
                Thumbnails = []
            },
            Player = new Player
            {
                VideoProgress = videoProgress,
                Queue = new XeroxDev.YTMDesktop.Companion.Models.Output.Queue
                {
                    Items = []
                }
            },
            PlaylistId = "playlist"
        };

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException($"Field '{fieldName}' was not found.");
        return (T)field.GetValue(instance)!;
    }

    private sealed class FakePlaybackController : IYtmPlaybackController
    {
        public bool IsAvailable => true;
        public StateOutput? LastKnownState => null;
        public List<string> ChangeVideoCalls { get; } = [];
        public Exception? ChangeVideoException { get; set; }

        public Task<StateOutput?> GetStateAsync() => Task.FromResult<StateOutput?>(null);

        public Task ChangeVideoAsync(string videoId)
        {
            ChangeVideoCalls.Add(videoId);
            if (ChangeVideoException is not null)
                throw ChangeVideoException;
            return Task.CompletedTask;
        }

        public Task NextAsync() => Task.CompletedTask;

        public Task PreviousAsync() => Task.CompletedTask;

        public Task SetVolumeAsync(int volume) => Task.CompletedTask;
    }
}
