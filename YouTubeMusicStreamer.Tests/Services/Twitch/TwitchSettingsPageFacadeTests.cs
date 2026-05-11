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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;
using YouTubeMusicStreamer.Services.App;
using YouTubeMusicStreamer.Services.App.Persistence;
using YouTubeMusicStreamer.Services.Commands;
using YouTubeMusicStreamer.Services.Commands.ArgumentParser;
using YouTubeMusicStreamer.Services.Commands.Binding;
using YouTubeMusicStreamer.Services.Commands.Cooldowns;
using YouTubeMusicStreamer.Services.Commands.Formatting;
using YouTubeMusicStreamer.Services.Commands.Placeholders;
using YouTubeMusicStreamer.Services.Commands.PrerequisiteChecking;
using YouTubeMusicStreamer.Services.Twitch;
using YouTubeMusicStreamer.Services.Twitch.Interfaces;
using YouTubeMusicStreamer.Tests.TestSupport;

namespace YouTubeMusicStreamer.Tests.Services.Twitch;

public sealed class TwitchSettingsPageFacadeTests
{
    [Fact]
    public async Task Initialize_LoadsSettingsAndCommandDescriptors_WhenFacadeStarts()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        var settings = harness.Settings;
        await settings.InitializeAsync();
        await settings.SaveTwitchSettingsAsync(new TwitchSettingsSnapshot(false, "Hello", "?", null, null));
        var commandService = await CreateCommandServiceAsync(settings);
        var facade = new TwitchSettingsPageFacade(settings, commandService);

        facade.Initialize();

        Assert.Equal("Hello", facade.Settings.ConnectMessage);
        Assert.Equal("?", facade.Settings.CommandPrefix);
        Assert.NotEmpty(facade.Commands);
        Assert.Contains(facade.Commands, x => x.Name == "RequestCommand");
    }

    [Fact]
    public async Task SaveSettingsAsync_UpdatesFacadeStateAndRaisesChange_WhenSettingsPersist()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        var settings = harness.Settings;
        await settings.InitializeAsync();
        var commandService = await CreateCommandServiceAsync(settings);
        var facade = new TwitchSettingsPageFacade(settings, commandService);
        var stateChangedCount = 0;
        facade.StateChanged += (_, _) => stateChangedCount++;

        facade.Initialize();
        await facade.SaveSettingsAsync(new TwitchSettingsSnapshot(false, "Updated", "#", null, null));

        Assert.Equal("Updated", facade.Settings.ConnectMessage);
        Assert.Equal("#", facade.Settings.CommandPrefix);
        Assert.Equal(1, stateChangedCount);
    }

    [Fact]
    public async Task Initialize_RefreshesCommandsAndRaisesChange_WhenCommandConfigurationChanges()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        var settings = harness.Settings;
        await settings.InitializeAsync();
        var commandService = await CreateCommandServiceAsync(settings);
        var facade = new TwitchSettingsPageFacade(settings, commandService);
        var stateChangedCount = 0;
        facade.StateChanged += (_, _) => stateChangedCount++;

        facade.Initialize();

        var request = facade.Commands.Single(x => x.Name == "RequestCommand");
        var updated = request.CommandConfiguration.Clone();
        updated.Trigger = "songrequest";

        await settings.SaveCommandConfigurationAsync("RequestCommand", updated);

        Assert.Contains(facade.Commands, x => x.Name == "RequestCommand" && x.CommandConfiguration.Trigger == "songrequest");
        Assert.Equal(1, stateChangedCount);
    }

    private static async Task<CommandService> CreateCommandServiceAsync(SettingsService settings)
    {
        var registry = new CommandRegistry(settings, new ReflectionPlaceholderProvider(), NullLogger<CommandRegistry>.Instance);
        var service = new CommandService(
            settings,
            registry,
            new ServiceCollection().BuildServiceProvider(),
            new NoOpTwitchChatService(),
            new ArgumentParser(),
            new PrerequisiteChecker(),
            new CooldownManager(),
            new ReflectionArgumentBinder(),
            new ResponseFormatter(),
            NullLogger<CommandService>.Instance);

        await service.InitializeAsync();
        return service;
    }

    private sealed class NoOpTwitchChatService : ITwitchChatService
    {
        public Task ConnectAsync(string username, string accessToken, string channelLogin) => Task.CompletedTask;
        public Task DisconnectAsync() => Task.CompletedTask;
        public void SendMessage(ChannelChatMessage senderMessage, string message, bool asReply = true) { }
    }
}
