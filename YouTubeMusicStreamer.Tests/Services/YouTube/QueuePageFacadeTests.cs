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

using YouTubeMusicStreamer.Models;
using YouTubeMusicStreamer.Services.App.Persistence;
using YouTubeMusicStreamer.Services.YouTube;
using YouTubeMusicStreamer.Tests.TestSupport;

namespace YouTubeMusicStreamer.Tests.Services.YouTube;

public sealed class QueuePageFacadeTests
{
    [Fact]
    public async Task Initialize_LoadsCurrentSettingsAndQueueItems_WhenFacadeStarts()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        var settings = harness.Settings;
        await settings.InitializeAsync();
        await settings.SaveQueueSettingsAsync(new QueueSettingsSnapshot(true));
        await settings.ReplaceQueueItemsAsync([
            CreateQueueItem("video-1", "UserA", "one"),
            CreateQueueItem("video-2", "UserB", "two")
        ]);

        var facade = new QueuePageFacade(settings);

        facade.Initialize();

        Assert.True(facade.Settings.QueueActive);
        Assert.Equal(["video-1", "video-2"], facade.QueueItems.Select(x => x.Id));
    }

    [Fact]
    public async Task SaveSettingsAsync_UpdatesFacadeStateAndRaisesChange_WhenSettingsPersist()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        var settings = harness.Settings;
        await settings.InitializeAsync();
        var facade = new QueuePageFacade(settings);
        var stateChangedCount = 0;
        facade.StateChanged += (_, _) => stateChangedCount++;

        facade.Initialize();
        await facade.SaveSettingsAsync(new QueueSettingsSnapshot(true));

        Assert.True(facade.Settings.QueueActive);
        Assert.Equal(1, stateChangedCount);
    }

    [Fact]
    public async Task RemoveItemAsync_RemovesMatchingQueueItem_WhenItemExists()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        var settings = harness.Settings;
        await settings.InitializeAsync();
        var itemOne = CreateQueueItem("video-1", "UserA", "one");
        var itemTwo = CreateQueueItem("video-2", "UserB", "two");
        await settings.ReplaceQueueItemsAsync([itemOne, itemTwo]);

        var facade = new QueuePageFacade(settings);
        facade.Initialize();

        await facade.RemoveItemAsync(itemOne);

        Assert.Equal(["video-2"], facade.QueueItems.Select(x => x.Id));
        Assert.Equal(["video-2"], settings.GetQueueItems().Select(x => x.Id));
    }

    [Fact]
    public async Task MoveItemUpAsync_ReordersQueue_WhenItemCanMoveHigher()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        var settings = harness.Settings;
        await settings.InitializeAsync();
        var itemOne = CreateQueueItem("video-1", "UserA", "one");
        var itemTwo = CreateQueueItem("video-2", "UserB", "two");
        var itemThree = CreateQueueItem("video-3", "UserC", "three");
        await settings.ReplaceQueueItemsAsync([itemOne, itemTwo, itemThree]);

        var facade = new QueuePageFacade(settings);
        facade.Initialize();

        await facade.MoveItemUpAsync(itemThree);

        Assert.Equal(["video-1", "video-3", "video-2"], facade.QueueItems.Select(x => x.Id));
    }

    [Fact]
    public async Task MoveItemToTopAsync_ReordersQueue_WhenItemExists()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        var settings = harness.Settings;
        await settings.InitializeAsync();
        var itemOne = CreateQueueItem("video-1", "UserA", "one");
        var itemTwo = CreateQueueItem("video-2", "UserB", "two");
        var itemThree = CreateQueueItem("video-3", "UserC", "three");
        await settings.ReplaceQueueItemsAsync([itemOne, itemTwo, itemThree]);

        var facade = new QueuePageFacade(settings);
        facade.Initialize();

        await facade.MoveItemToTopAsync(itemThree);

        Assert.Equal(["video-3", "video-1", "video-2"], facade.QueueItems.Select(x => x.Id));
    }

    [Fact]
    public async Task RemoveItemAsync_DoesNothing_WhenItemDoesNotExist()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        var settings = harness.Settings;
        await settings.InitializeAsync();
        var existing = CreateQueueItem("video-1", "UserA", "one");
        await settings.ReplaceQueueItemsAsync([existing]);

        var facade = new QueuePageFacade(settings);
        facade.Initialize();

        await facade.RemoveItemAsync(CreateQueueItem("video-x", "Other", "missing"));

        Assert.Equal(["video-1"], facade.QueueItems.Select(x => x.Id));
    }

    [Fact]
    public async Task MoveItemUpAsync_DoesNothing_WhenItemIsAlreadyAtTop()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        var settings = harness.Settings;
        await settings.InitializeAsync();
        var itemOne = CreateQueueItem("video-1", "UserA", "one");
        var itemTwo = CreateQueueItem("video-2", "UserB", "two");
        await settings.ReplaceQueueItemsAsync([itemOne, itemTwo]);

        var facade = new QueuePageFacade(settings);
        facade.Initialize();

        await facade.MoveItemUpAsync(itemOne);

        Assert.Equal(["video-1", "video-2"], facade.QueueItems.Select(x => x.Id));
    }

    private static QueueItem CreateQueueItem(string id, string requester, string message) =>
        new(id, requester, message)
        {
            RequestedAt = new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc).AddMinutes(id[^1] - '0')
        };
}
