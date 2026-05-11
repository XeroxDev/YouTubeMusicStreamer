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

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using YouTubeMusicStreamer.Models;
using YouTubeMusicStreamer.Services.App;
using YouTubeMusicStreamer.Services.App.Diagnostics;
using YouTubeMusicStreamer.Services.App.LegacySettings;
using YouTubeMusicStreamer.Services.App.Persistence;
using YouTubeMusicStreamer.Tests.TestSupport;

namespace YouTubeMusicStreamer.Tests.Services.App;

public sealed class SettingsServiceTests
{
    [Fact]
    public async Task InitializeAsync_CreatesDatabaseAndSeedsDefaultState_WhenDatabaseDoesNotExist()
    {
        await using var harness = SettingsPersistenceHarness.Create();

        await harness.Settings.InitializeAsync();

        Assert.True(File.Exists(harness.DatabaseFilePath));

        await using var connection = harness.CreateConnection();
        await connection.OpenAsync();

        Assert.Equal(1L, await ExecuteCountAsync(connection, "SELECT COUNT(*) FROM AppConfiguration;"));
        Assert.Equal(1L, await ExecuteCountAsync(connection, "SELECT COUNT(*) FROM YouTubeSettings;"));
        Assert.Equal(1L, await ExecuteCountAsync(connection, "SELECT COUNT(*) FROM TwitchSettings;"));
        Assert.Equal(1L, await ExecuteCountAsync(connection, "SELECT COUNT(*) FROM QueueSettings;"));
        Assert.True(await ExecuteCountAsync(connection, "SELECT COUNT(*) FROM __EFMigrationsHistory;") > 0);
    }

    [Fact]
    public async Task InitializeAsync_ReturnsSameTask_AndDoesNotDuplicateSeededRows_WhenCalledTwice()
    {
        await using var harness = SettingsPersistenceHarness.Create();

        var firstInitialization = harness.Settings.InitializeAsync();
        var secondInitialization = harness.Settings.InitializeAsync();

        Assert.Same(firstInitialization, secondInitialization);
        await Task.WhenAll(firstInitialization, secondInitialization);

        await using var connection = harness.CreateConnection();
        await connection.OpenAsync();

        Assert.Equal(1L, await ExecuteCountAsync(connection, "SELECT COUNT(*) FROM AppConfiguration;"));
        Assert.Equal(1L, await ExecuteCountAsync(connection, "SELECT COUNT(*) FROM YouTubeSettings;"));
        Assert.Equal(1L, await ExecuteCountAsync(connection, "SELECT COUNT(*) FROM TwitchSettings;"));
        Assert.Equal(1L, await ExecuteCountAsync(connection, "SELECT COUNT(*) FROM QueueSettings;"));
    }

    [Fact]
    public async Task InitializeAsync_ImportsLegacyJsonAndArchivesSourceFile_WhenStructuredDataDoesNotExist()
    {
        await using var harness = SettingsPersistenceHarness.Create();

        var requestedAt = new DateTime(2026, 4, 6, 12, 30, 0, DateTimeKind.Utc);
        var legacySettings = new LegacyAppSettings
        {
            LogLevel = LogLevel.Information,
            YouTubeHost = "127.0.0.1",
            YouTubePort = 9863,
            AutoStartServer = true,
            PublicPort = 9999,
            AllowAudioCapture = true,
            AudioCaptureDevice = "Loopback Device",
            TwitchSendMessageOnConnect = false,
            TwitchConnectMessage = "Legacy connected",
            TwitchCommandPrefix = "?",
            QueueActive = true
        };
        legacySettings.Blacklist.Add(new BlacklistEntry(new Uri("https://youtu.be/blocked-video"), "legacy block"));
        legacySettings.Queue.Add(new QueueItem("legacy-video", "LegacyUser", "legacy request", CreateEmbed("Legacy Song"))
        {
            RequestedAt = requestedAt
        });
        legacySettings.Commands["request"] = new LegacyCommandSettings
        {
            Trigger = "request",
            IsEnabled = true,
            RequiredBits = 250,
            Cooldown = 15,
            Response = "Legacy queued"
        };

        Directory.CreateDirectory(Path.GetDirectoryName(harness.LegacySettingsFilePath)!);
        var legacyJson = System.Text.Json.JsonSerializer.Serialize(legacySettings);
        await File.WriteAllTextAsync(harness.LegacySettingsFilePath, legacyJson);

        await harness.Settings.InitializeAsync();

        Assert.False(File.Exists(harness.LegacySettingsFilePath));
        Assert.True(File.Exists(harness.LegacySettingsArchiveFilePath));
        Assert.Equal(legacyJson, await File.ReadAllTextAsync(harness.LegacySettingsArchiveFilePath));

        Assert.Equal(LogLevel.Information, harness.Settings.GetAppConfiguration().LogLevel);
        Assert.Equal("127.0.0.1", harness.Settings.GetYouTubeSettings().Host);
        Assert.Equal(9863, harness.Settings.GetYouTubeSettings().Port);
        Assert.Equal("?", harness.Settings.GetTwitchSettings().CommandPrefix);
        Assert.True(harness.Settings.GetQueueSettings().QueueActive);

        var queueItems = harness.Settings.GetQueueItems();
        Assert.Single(queueItems);
        Assert.Equal("legacy-video", queueItems[0].Id);
        Assert.Equal(requestedAt, queueItems[0].RequestedAt);
        Assert.Equal("Legacy Song", queueItems[0].Embed!.Title);

        var blacklist = harness.Settings.GetBlacklistEntries();
        Assert.Single(blacklist);
        Assert.Equal("legacy block", blacklist[0].Description);

        var configuration = harness.Settings.GetCommandConfiguration("request");
        Assert.NotNull(configuration);
        Assert.Equal(ChatTriggerMode.BitsOnly, configuration!.ChatTriggerMode);
        Assert.Equal((uint)250, configuration.BitsThreshold);
        Assert.Equal("Legacy queued", configuration.Response);

        await using var connection = harness.CreateConnection();
        await connection.OpenAsync();

        Assert.Equal("127.0.0.1", await ExecuteScalarAsync<string>(connection, "SELECT Host FROM YouTubeSettings WHERE Id = 1;"));
        Assert.Equal(9863L, await ExecuteCountAsync(connection, "SELECT Port FROM YouTubeSettings WHERE Id = 1;"));
        Assert.Equal("?", await ExecuteScalarAsync<string>(connection, "SELECT CommandPrefix FROM TwitchSettings WHERE Id = 1;"));
        Assert.Equal(1L, await ExecuteCountAsync(connection, "SELECT QueueActive FROM QueueSettings WHERE Id = 1;"));
        Assert.Equal(1L, await ExecuteCountAsync(connection, "SELECT COUNT(*) FROM QueueItems WHERE VideoId = 'legacy-video';"));
        Assert.Equal(
            "legacy request",
            await ExecuteScalarAsync<string>(connection, "SELECT Message FROM QueueItems WHERE VideoId = 'legacy-video' LIMIT 1;"));
        Assert.Equal(
            "https://youtu.be/blocked-video",
            await ExecuteScalarAsync<string>(connection, "SELECT Url FROM BlacklistEntries LIMIT 1;"));
        Assert.Equal(
            250L,
            await ExecuteCountAsync(connection, "SELECT BitsThreshold FROM CommandConfigurations WHERE CommandKey = 'request';"));
        Assert.Equal(
            (long)ChatTriggerMode.BitsOnly,
            await ExecuteCountAsync(connection, "SELECT ChatTriggerMode FROM CommandConfigurations WHERE CommandKey = 'request';"));
    }

    [Fact]
    public async Task InitializeAsync_SeedsDefaultsAndArchivesInvalidLegacyFile_WhenLegacyJsonIsMalformed()
    {
        await using var harness = SettingsPersistenceHarness.Create();

        Directory.CreateDirectory(Path.GetDirectoryName(harness.LegacySettingsFilePath)!);
        const string invalidJson = "{ this is not valid json";
        await File.WriteAllTextAsync(harness.LegacySettingsFilePath, invalidJson);

        await harness.Settings.InitializeAsync();

        Assert.False(File.Exists(harness.LegacySettingsFilePath));
        Assert.False(File.Exists(harness.LegacySettingsArchiveFilePath));
        Assert.True(File.Exists(harness.LegacySettingsInvalidFilePath));
        Assert.Equal(invalidJson, await File.ReadAllTextAsync(harness.LegacySettingsInvalidFilePath));

        await using var connection = harness.CreateConnection();
        await connection.OpenAsync();

        Assert.Equal(1L, await ExecuteCountAsync(connection, "SELECT COUNT(*) FROM AppConfiguration;"));
        Assert.Equal(1L, await ExecuteCountAsync(connection, "SELECT COUNT(*) FROM YouTubeSettings;"));
        Assert.Equal(1L, await ExecuteCountAsync(connection, "SELECT COUNT(*) FROM TwitchSettings;"));
        Assert.Equal(1L, await ExecuteCountAsync(connection, "SELECT COUNT(*) FROM QueueSettings;"));
        Assert.Equal("!", await ExecuteScalarAsync<string>(connection, "SELECT CommandPrefix FROM TwitchSettings WHERE Id = 1;"));
        Assert.Equal(0L, await ExecuteCountAsync(connection, "SELECT COUNT(*) FROM QueueItems;"));
        Assert.Equal(0L, await ExecuteCountAsync(connection, "SELECT COUNT(*) FROM BlacklistEntries;"));

        Assert.Single(harness.Diagnostics.RecentDiagnostics);
        Assert.Contains(
            harness.Diagnostics.RecentDiagnostics,
            diagnostic => diagnostic.Category == AppDiagnosticCategory.Configuration &&
                          diagnostic.Summary == "Failed to load legacy AppSettings.json for migration");
    }

    [Fact]
    public async Task InitializeAsync_NormalizesMissingLegacyCommandPrefix_ToDefaultBangPrefix()
    {
        await using var harness = SettingsPersistenceHarness.Create();

        var legacySettings = new LegacyAppSettings
        {
            TwitchCommandPrefix = null!,
            TwitchConnectMessage = "Legacy connected"
        };

        Directory.CreateDirectory(Path.GetDirectoryName(harness.LegacySettingsFilePath)!);
        await File.WriteAllTextAsync(
            harness.LegacySettingsFilePath,
            System.Text.Json.JsonSerializer.Serialize(legacySettings));

        await harness.Settings.InitializeAsync();

        Assert.Equal("!", harness.Settings.GetTwitchSettings().CommandPrefix);

        await using var connection = harness.CreateConnection();
        await connection.OpenAsync();
        Assert.Equal("!", await ExecuteScalarAsync<string>(connection, "SELECT CommandPrefix FROM TwitchSettings WHERE Id = 1;"));
    }

    [Fact]
    public async Task SaveCommandConfigurationAsync_NormalizesChatAndBitsToChat_WhenBitsThresholdIsZero()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        await harness.Settings.InitializeAsync();

        await harness.Settings.SaveCommandConfigurationAsync("request", new CommandConfigurationSnapshot
        {
            Trigger = "request",
            ChatTriggerMode = ChatTriggerMode.ChatAndBits,
            RewardTriggerMode = RewardTriggerMode.Disabled,
            RequiredAccessLevel = CommandAccessLevel.Everyone,
            CooldownScope = CommandCooldownScope.Global,
            BitsThreshold = 0,
            Cooldown = 5,
            Response = "ok"
        });

        var configuration = harness.Settings.GetCommandConfiguration("request");

        Assert.NotNull(configuration);
        Assert.Equal(ChatTriggerMode.Chat, configuration!.ChatTriggerMode);

        var reloaded = harness.CreateReloadedService();
        await reloaded.InitializeAsync();

        var reloadedConfiguration = reloaded.GetCommandConfiguration("request");
        Assert.NotNull(reloadedConfiguration);
        Assert.Equal(ChatTriggerMode.Chat, reloadedConfiguration!.ChatTriggerMode);
    }

    [Fact]
    public async Task SaveCommandConfigurationAsync_NormalizesChatAndBitsToBitsOnly_WhenBitsThresholdIsPositive()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        await harness.Settings.InitializeAsync();

        await harness.Settings.SaveCommandConfigurationAsync("request", new CommandConfigurationSnapshot
        {
            Trigger = "request",
            ChatTriggerMode = ChatTriggerMode.ChatAndBits,
            RewardTriggerMode = RewardTriggerMode.Disabled,
            RequiredAccessLevel = CommandAccessLevel.Everyone,
            CooldownScope = CommandCooldownScope.PerUser,
            BitsThreshold = 250,
            Cooldown = 10,
            Response = "thanks"
        });

        var configuration = harness.Settings.GetCommandConfiguration("request");

        Assert.NotNull(configuration);
        Assert.Equal(ChatTriggerMode.BitsOnly, configuration!.ChatTriggerMode);
        Assert.Equal((uint)250, configuration.BitsThreshold);
    }

    [Fact]
    public async Task GetQueueItems_ReturnsClones_WhenCallerMutatesReturnedList()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        await harness.Settings.InitializeAsync();

        await harness.Settings.ReplaceQueueItemsAsync([
            new QueueItem("video-1", "UserA", "hello")
            {
                Embed = CreateEmbed("Song 1"),
                RequestedAt = new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc)
            }
        ]);

        var firstRead = harness.Settings.GetQueueItems();
        firstRead[0].Embed = CreateEmbed("Mutated");

        var secondRead = harness.Settings.GetQueueItems();

        Assert.Equal("Song 1", secondRead[0].Embed!.Title);
    }

    [Fact]
    public async Task ReplaceQueueItemsAsync_PersistsOrderAndEmbedJson_WhenServiceIsReloaded()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        await harness.Settings.InitializeAsync();

        await harness.Settings.ReplaceQueueItemsAsync([
            new QueueItem("video-2", "UserB", "second", CreateEmbed("Second Song"))
            {
                RequestedAt = new DateTime(2026, 4, 6, 12, 2, 0, DateTimeKind.Utc)
            },
            new QueueItem("video-1", "UserA", "first", CreateEmbed("First Song"))
            {
                RequestedAt = new DateTime(2026, 4, 6, 12, 1, 0, DateTimeKind.Utc)
            }
        ]);

        await using var connection = harness.CreateConnection();
        await connection.OpenAsync();
        var sortOrders = await ReadSortOrdersAsync(connection);
        Assert.Equal([0, 1], sortOrders);

        var persistedEmbedJson = await ExecuteScalarAsync<string>(connection, "SELECT EmbedJson FROM QueueItems WHERE SortOrder = 0;");
        Assert.Contains("\"title\":\"Second Song\"", persistedEmbedJson);

        var reloaded = harness.CreateReloadedService();
        await reloaded.InitializeAsync();

        var reloadedItems = reloaded.GetQueueItems();

        Assert.Equal(["video-2", "video-1"], reloadedItems.Select(x => x.Id));
        Assert.Equal("Second Song", reloadedItems[0].Embed!.Title);
        Assert.Equal("First Song", reloadedItems[1].Embed!.Title);
    }

    [Fact]
    public async Task UpdateQueueItemsAsync_PersistsUpdatedClone_WhenUpdaterMutatesWorkingList()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        await harness.Settings.InitializeAsync();

        await harness.Settings.ReplaceQueueItemsAsync([
            new QueueItem("video-1", "UserA", "original")
        ]);

        await harness.Settings.UpdateQueueItemsAsync(items =>
        {
            items[0] = new QueueItem("video-1", "UserA", "updated", CreateEmbed("Updated Song"));
            items.Add(new QueueItem("video-2", "UserB", "new"));
        });

        var items = harness.Settings.GetQueueItems();

        Assert.Equal(2, items.Count);
        Assert.Equal("updated", items[0].Message);
        Assert.Equal("Updated Song", items[0].Embed!.Title);
        Assert.Equal("video-2", items[1].Id);
    }

    [Fact]
    public async Task ReplaceBlacklistEntriesAsync_PersistsEntries_WhenServiceIsReloaded()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        await harness.Settings.InitializeAsync();

        await harness.Settings.ReplaceBlacklistEntriesAsync([
            new BlacklistEntry(new Uri("https://youtu.be/video-1"), "duplicate"),
            new BlacklistEntry(new Uri("https://www.youtube.com/watch?v=video-2"), "blocked")
        ]);

        var reloaded = harness.CreateReloadedService();
        await reloaded.InitializeAsync();

        var entries = reloaded.GetBlacklistEntries();

        Assert.Equal(2, entries.Count);
        Assert.Equal("https://youtu.be/video-1", entries[0].Url.ToString());
        Assert.Equal("duplicate", entries[0].Description);
        Assert.Equal("blocked", entries[1].Description);
    }

    [Fact]
    public async Task SaveCommandConfigurationAsync_RoundTripsRewardBinding_WhenServiceIsReloaded()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        await harness.Settings.InitializeAsync();

        await harness.Settings.SaveCommandConfigurationAsync("request", new CommandConfigurationSnapshot
        {
            Trigger = "request",
            ChatTriggerMode = ChatTriggerMode.Disabled,
            RewardTriggerMode = RewardTriggerMode.AppManaged,
            RequiredAccessLevel = CommandAccessLevel.Moderator,
            CooldownScope = CommandCooldownScope.PerUser,
            BitsThreshold = 0,
            RewardBinding = new CommandRewardBindingSnapshot
            {
                RewardId = "reward-1",
                BroadcasterAccountId = "broadcaster-1",
                ManagedRewardName = "Song Request",
                ManagedRewardPrompt = "Drop a URL",
                ManagedRewardCost = 150,
                ManagedRewardRequiresUserInput = true,
                ManagedRewardCompatibilityState = ManagedRewardCompatibilityState.Compatible,
                ManagedRewardCompatibilityMessage = "ok"
            },
            Cooldown = 30,
            Response = "queued",
            AccessDeniedResponse = "denied"
        });

        var reloaded = harness.CreateReloadedService();
        await reloaded.InitializeAsync();

        var configuration = reloaded.GetCommandConfiguration("request");

        Assert.NotNull(configuration);
        Assert.NotNull(configuration!.RewardBinding);
        Assert.Equal(RewardTriggerMode.AppManaged, configuration.RewardTriggerMode);
        Assert.Equal("reward-1", configuration.RewardBinding!.RewardId);
        Assert.Equal("Song Request", configuration.RewardBinding.ManagedRewardName);
        Assert.Equal((uint)150, configuration.RewardBinding.ManagedRewardCost);
        Assert.True(configuration.RewardBinding.ManagedRewardRequiresUserInput);
        Assert.Equal(ManagedRewardCompatibilityState.Compatible, configuration.RewardBinding.ManagedRewardCompatibilityState);
        Assert.Equal("denied", configuration.AccessDeniedResponse);
    }

    [Fact]
    public async Task QueueItemsChanged_RaisesUpdatedClone_WhenQueueIsReplaced()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        await harness.Settings.InitializeAsync();

        IReadOnlyList<QueueItem>? raisedItems = null;
        harness.Settings.QueueItemsChanged += items => raisedItems = items;

        await harness.Settings.ReplaceQueueItemsAsync([
            new QueueItem("video-1", "UserA", "hello")
        ]);

        Assert.NotNull(raisedItems);
        Assert.Single(raisedItems!);
        Assert.NotSame(raisedItems, harness.Settings.GetQueueItems());
        Assert.Equal("video-1", raisedItems[0].Id);
    }

    [Fact]
    public async Task SaveYouTubeSettingsAsync_RecordsDiagnosticAndRethrows_WhenPersistenceFails()
    {
        var diagnostics = new RecordingDiagnosticsService();
        var settings = SettingsServiceTestSupport.CreateWithThrowingDb(diagnostics: diagnostics);

        var exception = await Assert.ThrowsAsync<NotSupportedException>(() =>
            settings.SaveYouTubeSettingsAsync(new YouTubeSettingsSnapshot("127.0.0.1", 9863, true, 9876, false, string.Empty)));

        Assert.IsType<NotSupportedException>(exception);
        var diagnostic = Assert.Single(diagnostics.RecentDiagnostics);
        Assert.Equal(AppDiagnosticCategory.Persistence, diagnostic.Category);
        Assert.Equal(AppDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("Failed to persist settings update", diagnostic.Summary);
    }

    private static YouTubeEmbed CreateEmbed(string title) => new(
        title,
        "Artist",
        "https://example.com/artist",
        "video",
        300,
        400,
        "1.0",
        "YouTube",
        "https://youtube.com",
        300,
        400,
        "https://img.youtube.com/test.jpg",
        "<iframe></iframe>");

    private static async Task<long> ExecuteCountAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<T> ExecuteScalarAsync<T>(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (T)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<IReadOnlyList<int>> ReadSortOrdersAsync(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT SortOrder FROM QueueItems ORDER BY SortOrder;";

        var results = new List<int>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            results.Add(reader.GetInt32(0));

        return results;
    }
}
