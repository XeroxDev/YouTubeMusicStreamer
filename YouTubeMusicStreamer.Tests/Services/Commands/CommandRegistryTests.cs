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
using YouTubeMusicStreamer.Services.App;
using YouTubeMusicStreamer.Services.App.Persistence;
using YouTubeMusicStreamer.Services.Commands;
using YouTubeMusicStreamer.Services.Commands.Placeholders;
using YouTubeMusicStreamer.Tests.TestSupport;

namespace YouTubeMusicStreamer.Tests.Services.Commands;

public class CommandRegistryTests
{
    [Fact]
    public async Task InitializeAsync_DiscoversCommandsAndPersistsDefaultConfiguration_WhenSettingsWereEmpty()
    {
        await using var fixture = CreateFixture();

        await fixture.Registry.InitializeAsync();

        Assert.True(fixture.Registry.IsInitialized);
        Assert.True(fixture.Registry.TryGetByName("ListCommand", out var entry));
        Assert.Equal("list", entry.DefaultTrigger);

        var configuration = fixture.Settings.GetCommandConfiguration("ListCommand");
        Assert.NotNull(configuration);
        Assert.Equal("list", configuration!.Trigger);
        Assert.Equal(ChatTriggerMode.Chat, configuration.ChatTriggerMode);
        Assert.Equal(30u, configuration.Cooldown);
        Assert.Equal("Available commands: {commands}", configuration.Response);
    }

    [Fact]
    public async Task InitializeAsync_UsesDisabledDefaultTriggerMode_WhenCommandIsDisabledByAttribute()
    {
        await using var fixture = CreateFixture();

        await fixture.Registry.InitializeAsync();

        var configuration = fixture.Settings.GetCommandConfiguration("VolumeCommand");
        Assert.NotNull(configuration);
        Assert.Equal(ChatTriggerMode.Disabled, configuration!.ChatTriggerMode);
        Assert.Equal(300u, configuration.Cooldown);
    }

    [Fact]
    public async Task GetCommandDescriptors_ReturnsBitsAndRewardInvocations_WhenConfigurationEnablesBoth()
    {
        await using var fixture = CreateFixture(new Dictionary<string, CommandConfigurationSnapshot>(StringComparer.Ordinal)
        {
            ["RequestCommand"] = new()
            {
                Trigger = "song",
                ChatTriggerMode = ChatTriggerMode.BitsOnly,
                RewardTriggerMode = RewardTriggerMode.Existing,
                RequiredAccessLevel = CommandAccessLevel.Everyone,
                CooldownScope = CommandCooldownScope.Global,
                BitsThreshold = 250,
                RewardBinding = new CommandRewardBindingSnapshot
                {
                    RewardId = "reward-1",
                    ManagedRewardName = "Song Request"
                },
                Cooldown = 120,
                Response = "Added {title}"
            }
        });

        await fixture.Registry.InitializeAsync();

        var descriptor = Assert.Single(fixture.Registry.GetCommandDescriptors(), x => x.Name == "RequestCommand");

        Assert.Equal("song", descriptor.CommandConfiguration.Trigger);
        Assert.Equal(
            ["!song (250 bits)", "reward \"Song Request\""],
            descriptor.Invocations.Select(x => x.DisplayText).ToArray());
    }

    [Fact]
    public async Task RefreshTriggerMap_UsesUpdatedTriggerLookup_WhenConfigurationWasChanged()
    {
        await using var fixture = CreateFixture();
        await fixture.Registry.InitializeAsync();

        var configuration = fixture.Settings.GetCommandConfiguration("RequestCommand")!;
        configuration.Trigger = "song";
        await fixture.Settings.SaveCommandConfigurationAsync("RequestCommand", configuration);

        fixture.Registry.RefreshTriggerMap();

        Assert.True(fixture.Registry.TryGetByChatTrigger("song", out var updatedEntry));
        Assert.Equal("RequestCommand", updatedEntry.Name);
        Assert.False(fixture.Registry.TryGetByChatTrigger("request", out _));
    }

    [Fact]
    public async Task TryGetByRewardId_ReturnsConfiguredCommand_WhenRewardBindingMatches()
    {
        await using var fixture = CreateFixture(new Dictionary<string, CommandConfigurationSnapshot>(StringComparer.Ordinal)
        {
            ["RequestCommand"] = new()
            {
                Trigger = "request",
                ChatTriggerMode = ChatTriggerMode.Chat,
                RewardTriggerMode = RewardTriggerMode.Existing,
                RequiredAccessLevel = CommandAccessLevel.Everyone,
                CooldownScope = CommandCooldownScope.Global,
                BitsThreshold = 0,
                RewardBinding = new CommandRewardBindingSnapshot
                {
                    RewardId = "reward-1"
                },
                Cooldown = 120,
                Response = "Added {title}"
            }
        });

        await fixture.Registry.InitializeAsync();

        Assert.True(fixture.Registry.TryGetByRewardId("reward-1", out var entry));
        Assert.Equal("RequestCommand", entry.Name);
    }

    [Fact]
    public async Task RefreshTriggerMap_UsesLastConfiguredCommand_WhenTwoCommandsShareSameTrigger()
    {
        await using var fixture = CreateFixture(new Dictionary<string, CommandConfigurationSnapshot>(StringComparer.Ordinal)
        {
            ["InfoCommand"] = new()
            {
                Trigger = "shared",
                ChatTriggerMode = ChatTriggerMode.Chat,
                RewardTriggerMode = RewardTriggerMode.Disabled,
                RequiredAccessLevel = CommandAccessLevel.Everyone,
                CooldownScope = CommandCooldownScope.Global,
                BitsThreshold = 0,
                Cooldown = 60,
                Response = "Info"
            },
            ["RequestCommand"] = new()
            {
                Trigger = "shared",
                ChatTriggerMode = ChatTriggerMode.Chat,
                RewardTriggerMode = RewardTriggerMode.Disabled,
                RequiredAccessLevel = CommandAccessLevel.Everyone,
                CooldownScope = CommandCooldownScope.Global,
                BitsThreshold = 0,
                Cooldown = 120,
                Response = "Request"
            }
        });

        await fixture.Registry.InitializeAsync();

        Assert.True(fixture.Registry.TryGetByChatTrigger("shared", out var entry));
        Assert.Equal("RequestCommand", entry.Name);
    }

    [Fact]
    public async Task GetEnabledCommandListing_FormatsMixedInvocationShapes_WhenCommandsUseChatBitsAndRewards()
    {
        await using var fixture = CreateFixture(new Dictionary<string, CommandConfigurationSnapshot>(StringComparer.Ordinal)
        {
            ["InfoCommand"] = new()
            {
                Trigger = "info",
                ChatTriggerMode = ChatTriggerMode.Chat,
                RewardTriggerMode = RewardTriggerMode.Disabled,
                RequiredAccessLevel = CommandAccessLevel.Everyone,
                CooldownScope = CommandCooldownScope.Global,
                BitsThreshold = 0,
                Cooldown = 60,
                Response = "Info"
            },
            ["RequestCommand"] = new()
            {
                Trigger = "song",
                ChatTriggerMode = ChatTriggerMode.BitsOnly,
                RewardTriggerMode = RewardTriggerMode.Existing,
                RequiredAccessLevel = CommandAccessLevel.Everyone,
                CooldownScope = CommandCooldownScope.Global,
                BitsThreshold = 250,
                RewardBinding = new CommandRewardBindingSnapshot
                {
                    RewardId = "reward-1",
                    ManagedRewardName = "Song Request"
                },
                Cooldown = 120,
                Response = "Added {title}"
            }
        });

        await fixture.Registry.InitializeAsync();

        var listing = fixture.Registry.GetEnabledCommandListing();

        Assert.Contains("Info [!info]", listing);
        Assert.Contains("Request [!song (250 bits), reward \"Song Request\"]", listing);
        Assert.DoesNotContain(listing, item => item.StartsWith("Volume [", StringComparison.Ordinal));
    }

    [Fact]
    public async Task InitializeAsync_PreservesStoredConfiguration_WhenCommandConfigurationAlreadyExists()
    {
        await using var fixture = CreateFixture(new Dictionary<string, CommandConfigurationSnapshot>(StringComparer.Ordinal)
        {
            ["RequestCommand"] = new()
            {
                Trigger = "mysong",
                ChatTriggerMode = ChatTriggerMode.Chat,
                RewardTriggerMode = RewardTriggerMode.Disabled,
                RequiredAccessLevel = CommandAccessLevel.Moderator,
                CooldownScope = CommandCooldownScope.PerUser,
                BitsThreshold = 0,
                Cooldown = 9,
                Response = "Custom {title}"
            }
        });

        await fixture.Registry.InitializeAsync();

        var configuration = fixture.Settings.GetCommandConfiguration("RequestCommand");
        Assert.NotNull(configuration);
        Assert.Equal("mysong", configuration!.Trigger);
        Assert.Equal(CommandAccessLevel.Moderator, configuration.RequiredAccessLevel);
        Assert.Equal(CommandCooldownScope.PerUser, configuration.CooldownScope);
        Assert.Equal((uint)9, configuration.Cooldown);
        Assert.Equal("Custom {title}", configuration.Response);
    }

    [Fact]
    public async Task TryGetByChatTrigger_IsCaseInsensitive_WhenTriggerCasingDiffers()
    {
        await using var fixture = CreateFixture(new Dictionary<string, CommandConfigurationSnapshot>(StringComparer.Ordinal)
        {
            ["RequestCommand"] = new()
            {
                Trigger = "Song",
                ChatTriggerMode = ChatTriggerMode.Chat,
                RewardTriggerMode = RewardTriggerMode.Disabled,
                RequiredAccessLevel = CommandAccessLevel.Everyone,
                CooldownScope = CommandCooldownScope.Global,
                BitsThreshold = 0,
                Cooldown = 120,
                Response = "Added {title}"
            }
        });

        await fixture.Registry.InitializeAsync();

        Assert.True(fixture.Registry.TryGetByChatTrigger("song", out var lower));
        Assert.True(fixture.Registry.TryGetByChatTrigger("SONG", out var upper));
        Assert.Equal("RequestCommand", lower.Name);
        Assert.Equal("RequestCommand", upper.Name);
    }

    private static RegistryFixture CreateFixture(IReadOnlyDictionary<string, CommandConfigurationSnapshot>? commandConfigurations = null)
    {
        var dbFactory = new InMemorySqliteDbContextFactory();
        var settings = SettingsServiceTestSupport.CreateWithInMemoryDb(
            dbFactory,
            twitchSettings: new TwitchSettingsSnapshot(true, "Connected!", "!", null, null),
            commandConfigurations: commandConfigurations ?? new Dictionary<string, CommandConfigurationSnapshot>(StringComparer.Ordinal));

        var registry = new CommandRegistry(
            settings,
            new ReflectionPlaceholderProvider(),
            NullLogger<CommandRegistry>.Instance);

        return new RegistryFixture(settings, registry, dbFactory);
    }

    private sealed class RegistryFixture(
        SettingsService settings,
        CommandRegistry registry,
        InMemorySqliteDbContextFactory dbFactory) : IAsyncDisposable
    {
        public SettingsService Settings { get; } = settings;
        public CommandRegistry Registry { get; } = registry;

        public ValueTask DisposeAsync() => dbFactory.DisposeAsync();
    }
}
