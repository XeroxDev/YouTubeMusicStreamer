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

using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TwitchLib.EventSub.Core.Models.Chat;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;
using YouTubeMusicStreamer.Attributes;
using YouTubeMusicStreamer.Services.App.Persistence;
using YouTubeMusicStreamer.Services.Commands;
using YouTubeMusicStreamer.Services.Commands.ArgumentParser;
using YouTubeMusicStreamer.Services.Commands.Binding;
using YouTubeMusicStreamer.Services.Commands.Cooldowns;
using YouTubeMusicStreamer.Services.Commands.Formatting;
using YouTubeMusicStreamer.Services.Commands.Placeholders;
using YouTubeMusicStreamer.Services.Commands.PrerequisiteChecking;
using YouTubeMusicStreamer.Services.Twitch.Interfaces;
using YouTubeMusicStreamer.Tests.TestSupport;

namespace YouTubeMusicStreamer.Tests.Services.Commands;

public class CommandServiceTests
{
    [Fact]
    public async Task ProcessInputAsync_ReturnsSkippedNotInitialized_WhenRegistryWasNotInitialized()
    {
        var fixture = CreateFixture(registryInitialized: false);

        var outcome = await fixture.Service.ProcessInputAsync(CreateMessage("!request song"));

        Assert.Equal(CommandExecutionStatus.Skipped, outcome.Status);
        Assert.Equal(CommandExecutionReason.NotInitialized, outcome.Reason);
        Assert.Equal("Command registry is not initialized.", outcome.Message);
        Assert.Null(fixture.Binder.LastContext);
        Assert.Empty(fixture.Chat.Messages);
    }

    [Fact]
    public async Task ProcessInputAsync_ReturnsSkippedInvalidConfiguration_WhenStoredConfigurationIsMissing()
    {
        var fixture = CreateFixture(commandConfigurations: new Dictionary<string, CommandConfigurationSnapshot>());

        var outcome = await fixture.Service.ProcessInputAsync(CreateMessage("!request song"));

        Assert.Equal(CommandExecutionStatus.Skipped, outcome.Status);
        Assert.Equal(CommandExecutionReason.InvalidConfiguration, outcome.Reason);
        Assert.Contains("No configuration found", outcome.Message);
        Assert.Null(fixture.Binder.LastContext);
        Assert.Empty(fixture.Chat.Messages);
    }

    [Fact]
    public async Task ProcessInputAsync_UsesBitsInvocationKind_WhenCheerObjectContainsBits()
    {
        var fixture = CreateFixture();
        fixture.PrerequisiteChecker.Result = new CommandEligibilityResult(true, CommandExecutionStatus.Fulfilled, CommandExecutionReason.None);
        fixture.Binder.ResultFactory = context => CommandResult.Fulfilled();

        var outcome = await fixture.Service.ProcessInputAsync(CreateMessage("!request song", cheerBits: 250));

        Assert.Equal(CommandInvocationKind.Bits, outcome.InvocationKind);
        Assert.Equal(250, fixture.PrerequisiteChecker.LastBits);
        Assert.Equal(CommandInvocationKind.Bits, fixture.PrerequisiteChecker.LastInvocationKind);
        Assert.Equal(250, fixture.Binder.LastContext!.Bits);
        Assert.Equal(CommandInvocationKind.Bits, fixture.Binder.LastContext.InvocationKind);
    }

    [Fact]
    public async Task ProcessInputAsync_UsesBitsInvocationKind_WhenCheerBitsExistOnlyInFragments()
    {
        var fixture = CreateFixture();
        fixture.PrerequisiteChecker.Result = new CommandEligibilityResult(true, CommandExecutionStatus.Fulfilled, CommandExecutionReason.None);
        fixture.Binder.ResultFactory = context => CommandResult.Fulfilled();

        var outcome = await fixture.Service.ProcessInputAsync(CreateMessage("!request song", fragmentBits: [100, 25]));

        Assert.Equal(CommandInvocationKind.Bits, outcome.InvocationKind);
        Assert.Equal(125, fixture.PrerequisiteChecker.LastBits);
        Assert.Equal(125, fixture.Binder.LastContext!.Bits);
    }

    [Fact]
    public async Task ProcessInputAsync_UsesHigherFragmentBits_WhenFragmentBitsExceedCheerSummary()
    {
        var fixture = CreateFixture(commandConfiguration: CreateCommandConfiguration(response: "Spent {bits} bits"));
        fixture.PrerequisiteChecker.Result = new CommandEligibilityResult(true, CommandExecutionStatus.Fulfilled, CommandExecutionReason.None);
        fixture.Binder.ResultFactory = context => CommandResult.Fulfilled();

        var outcome = await fixture.Service.ProcessInputAsync(CreateMessage("!request song", cheerBits: 100, fragmentBits: [100, 50], messageId: "reply-id"));

        Assert.Equal(CommandInvocationKind.Bits, outcome.InvocationKind);
        Assert.Equal(150, fixture.PrerequisiteChecker.LastBits);
        Assert.Equal(150, fixture.Binder.LastContext!.Bits);
        Assert.Equal("150", outcome.Placeholders["{bits}"]);
        Assert.Equal("Spent 150 bits", Assert.Single(fixture.Chat.Messages).Message);
    }

    [Fact]
    public async Task ProcessInputAsync_UsesHigherCheerBits_WhenCheerSummaryExceedsFragmentBits()
    {
        var fixture = CreateFixture(commandConfiguration: CreateCommandConfiguration(response: "Spent {bits} bits"));
        fixture.PrerequisiteChecker.Result = new CommandEligibilityResult(true, CommandExecutionStatus.Fulfilled, CommandExecutionReason.None);
        fixture.Binder.ResultFactory = context => CommandResult.Fulfilled();

        var outcome = await fixture.Service.ProcessInputAsync(CreateMessage("!request song", cheerBits: 500, fragmentBits: [100, 25], messageId: "reply-id"));

        Assert.Equal(CommandInvocationKind.Bits, outcome.InvocationKind);
        Assert.Equal(500, fixture.PrerequisiteChecker.LastBits);
        Assert.Equal(500, fixture.Binder.LastContext!.Bits);
        Assert.Equal("500", outcome.Placeholders["{bits}"]);
        Assert.Equal("Spent 500 bits", Assert.Single(fixture.Chat.Messages).Message);
    }

    [Fact]
    public async Task ProcessInputAsync_ShouldHandle_WhenMillionaireSpendsEntireLifeSavingsInBits()
    {
        // If this really happens and Twitch passes it through, I think the streamer can easily fix this themselves.
        var fixture = CreateFixture(commandConfiguration: CreateCommandConfiguration(response: "Spent {bits} bits"));
        fixture.PrerequisiteChecker.Result = new CommandEligibilityResult(true, CommandExecutionStatus.Fulfilled, CommandExecutionReason.None);
        fixture.Binder.ResultFactory = context => CommandResult.Fulfilled();

        var outcome = await fixture.Service.ProcessInputAsync(CreateMessage("!request song", fragmentBits: [int.MaxValue, int.MaxValue], messageId: "reply-id"));

        Assert.Equal(CommandInvocationKind.Bits, outcome.InvocationKind);
        Assert.Equal(int.MaxValue, fixture.PrerequisiteChecker.LastBits);
        Assert.Equal(int.MaxValue, fixture.Binder.LastContext!.Bits);
        Assert.Equal(int.MaxValue.ToString(), outcome.Placeholders["{bits}"]);
        Assert.Equal($"Spent {int.MaxValue} bits", Assert.Single(fixture.Chat.Messages).Message);
    }

    [Fact]
    public async Task ProcessInputAsync_UsesChatInvocationKind_WhenNoBitsArePresent()
    {
        var fixture = CreateFixture();
        fixture.PrerequisiteChecker.Result = new CommandEligibilityResult(true, CommandExecutionStatus.Fulfilled, CommandExecutionReason.None);
        fixture.Binder.ResultFactory = context => CommandResult.Fulfilled();

        var outcome = await fixture.Service.ProcessInputAsync(CreateMessage("!request song"));

        Assert.Equal(CommandInvocationKind.Chat, outcome.InvocationKind);
        Assert.Equal(0, fixture.PrerequisiteChecker.LastBits);
        Assert.Equal(CommandInvocationKind.Chat, fixture.PrerequisiteChecker.LastInvocationKind);
    }

    [Fact]
    public async Task ProcessInputAsync_ExposesBitsPlaceholder_InOutcomeAndFormattedResponse()
    {
        var fixture = CreateFixture(commandConfiguration: CreateCommandConfiguration(response: "Spent {bits} bits"));
        fixture.PrerequisiteChecker.Result = new CommandEligibilityResult(true, CommandExecutionStatus.Fulfilled, CommandExecutionReason.None);
        fixture.Binder.ResultFactory = context => CommandResult.Fulfilled();

        var outcome = await fixture.Service.ProcessInputAsync(CreateMessage("!request song", cheerBits: 333, messageId: "reply-id"));

        Assert.Equal("333", outcome.Placeholders["{bits}"]);
        Assert.Equal("Spent 333 bits", Assert.Single(fixture.Chat.Messages).Message);
        Assert.True(Assert.Single(fixture.Chat.Messages).AsReply);
    }

    [Fact]
    public async Task ProcessInputAsync_UsesZeroBitsPlaceholder_WhenMessageWasNotPaid()
    {
        var fixture = CreateFixture(commandConfiguration: CreateCommandConfiguration(response: "Spent {bits} bits"));
        fixture.PrerequisiteChecker.Result = new CommandEligibilityResult(true, CommandExecutionStatus.Fulfilled, CommandExecutionReason.None);
        fixture.Binder.ResultFactory = context => CommandResult.Fulfilled();

        var outcome = await fixture.Service.ProcessInputAsync(CreateMessage("!request song", messageId: "reply-id"));

        Assert.Equal("0", outcome.Placeholders["{bits}"]);
        Assert.Equal("Spent 0 bits", Assert.Single(fixture.Chat.Messages).Message);
    }

    [Fact]
    public async Task ProcessInputAsync_UsesFragmentBitsPlaceholder_WhenCheerObjectIsMissing()
    {
        var fixture = CreateFixture(commandConfiguration: CreateCommandConfiguration(response: "Spent {bits} bits"));
        fixture.PrerequisiteChecker.Result = new CommandEligibilityResult(true, CommandExecutionStatus.Fulfilled, CommandExecutionReason.None);
        fixture.Binder.ResultFactory = context => CommandResult.Fulfilled();

        var outcome = await fixture.Service.ProcessInputAsync(CreateMessage("!request song", fragmentBits: [100, 25], messageId: "reply-id"));

        Assert.Equal(CommandInvocationKind.Bits, outcome.InvocationKind);
        Assert.Equal("125", outcome.Placeholders["{bits}"]);
        Assert.Equal("Spent 125 bits", Assert.Single(fixture.Chat.Messages).Message);
    }

    [Fact]
    public async Task ProcessInputAsync_ReturnsBlockedOutcomeWithoutBinding_WhenBitsCommandFailsPrerequisites()
    {
        var fixture = CreateFixture();
        fixture.PrerequisiteChecker.Result = new CommandEligibilityResult(
            false,
            CommandExecutionStatus.Blocked,
            CommandExecutionReason.BitsThresholdNotMet,
            "This command requires at least 500 bits.");

        var outcome = await fixture.Service.ProcessInputAsync(CreateMessage("!request song", cheerBits: 250, messageId: "reply-id"));

        Assert.Equal(CommandExecutionStatus.Blocked, outcome.Status);
        Assert.Equal(CommandExecutionReason.BitsThresholdNotMet, outcome.Reason);
        Assert.Equal("This command requires at least 500 bits.", outcome.Message);
        Assert.Equal(CommandInvocationKind.Bits, outcome.InvocationKind);
        Assert.Null(fixture.Binder.LastContext);
        Assert.Equal("This command requires at least 500 bits.", Assert.Single(fixture.Chat.Messages).Message);
    }

    [Fact]
    public async Task ProcessInputAsync_UsesConfiguredAccessDeniedResponse_WhenBitsCommandFailsAccessCheck()
    {
        var fixture = CreateFixture(commandConfiguration: CreateCommandConfiguration(accessDeniedResponse: "Denied for {username}"));
        fixture.PrerequisiteChecker.Result = new CommandEligibilityResult(
            false,
            CommandExecutionStatus.Blocked,
            CommandExecutionReason.AccessDenied,
            "This command requires moderator access.");

        var outcome = await fixture.Service.ProcessInputAsync(CreateMessage("!request song", cheerBits: 200, messageId: "reply-id"));

        Assert.Equal(CommandExecutionStatus.Blocked, outcome.Status);
        Assert.Equal(CommandExecutionReason.AccessDenied, outcome.Reason);
        Assert.Equal("This command requires moderator access.", outcome.Message);
        Assert.Null(fixture.Binder.LastContext);
        Assert.Equal("Denied for User", Assert.Single(fixture.Chat.Messages).Message);
        Assert.True(Assert.Single(fixture.Chat.Messages).AsReply);
    }

    [Fact]
    public async Task ProcessInputAsync_UsesEligibilityMessage_WhenAccessDeniedResponseWasNotConfigured()
    {
        var fixture = CreateFixture();
        fixture.PrerequisiteChecker.Result = new CommandEligibilityResult(
            false,
            CommandExecutionStatus.Blocked,
            CommandExecutionReason.AccessDenied,
            "This command requires moderator access.");

        var outcome = await fixture.Service.ProcessInputAsync(CreateMessage("!request song", messageId: "reply-id"));

        Assert.Equal(CommandExecutionStatus.Blocked, outcome.Status);
        Assert.Equal(CommandExecutionReason.AccessDenied, outcome.Reason);
        Assert.Equal("This command requires moderator access.", Assert.Single(fixture.Chat.Messages).Message);
        Assert.True(Assert.Single(fixture.Chat.Messages).AsReply);
        Assert.Null(fixture.Binder.LastContext);
    }

    [Fact]
    public async Task ProcessInputAsync_ReturnsCooldownBlockedOutcome_WhenCooldownIsActive()
    {
        var fixture = CreateFixture();
        fixture.PrerequisiteChecker.Result = new CommandEligibilityResult(true, CommandExecutionStatus.Fulfilled, CommandExecutionReason.None);
        fixture.CooldownManager.CanStart = false;
        fixture.CooldownManager.WaitSeconds = 17;

        var outcome = await fixture.Service.ProcessInputAsync(CreateMessage("!request song", messageId: "reply-id"));

        Assert.Equal(CommandExecutionStatus.Blocked, outcome.Status);
        Assert.Equal(CommandExecutionReason.CooldownActive, outcome.Reason);
        Assert.Equal(17, outcome.CooldownRemainingSeconds);
        Assert.Equal("Please wait 17s…", outcome.Message);
        Assert.Equal("Please wait 17s…", Assert.Single(fixture.Chat.Messages).Message);
        Assert.True(Assert.Single(fixture.Chat.Messages).AsReply);
        Assert.Null(fixture.Binder.LastContext);
    }

    [Fact]
    public async Task ProcessInputAsync_SendsFeedback_WhenBinderReturnsBadInputWithMessage()
    {
        var fixture = CreateFixture();
        fixture.PrerequisiteChecker.Result = new CommandEligibilityResult(true, CommandExecutionStatus.Fulfilled, CommandExecutionReason.None);
        fixture.Binder.ResultFactory = _ => CommandResult.BadInput(CommandExecutionReason.MissingArgument, "Missing song title.");

        var outcome = await fixture.Service.ProcessInputAsync(CreateMessage("!request", messageId: "reply-id"));

        Assert.Equal(CommandExecutionStatus.BadInput, outcome.Status);
        Assert.Equal(CommandExecutionReason.MissingArgument, outcome.Reason);
        Assert.Equal("Missing song title.", Assert.Single(fixture.Chat.Messages).Message);
        Assert.True(Assert.Single(fixture.Chat.Messages).AsReply);
    }

    [Fact]
    public async Task ProcessInputAsync_SendsFeedback_WhenBinderReturnsBlockedWithMessage()
    {
        var fixture = CreateFixture();
        fixture.PrerequisiteChecker.Result = new CommandEligibilityResult(true, CommandExecutionStatus.Fulfilled, CommandExecutionReason.None);
        fixture.Binder.ResultFactory = _ => CommandResult.Blocked(CommandExecutionReason.InvalidConfiguration, "Command misconfigured.");

        var outcome = await fixture.Service.ProcessInputAsync(CreateMessage("!request song", messageId: "reply-id"));

        Assert.Equal(CommandExecutionStatus.Blocked, outcome.Status);
        Assert.Equal(CommandExecutionReason.InvalidConfiguration, outcome.Reason);
        Assert.Equal("Command misconfigured.", Assert.Single(fixture.Chat.Messages).Message);
        Assert.True(Assert.Single(fixture.Chat.Messages).AsReply);
    }

    [Fact]
    public async Task ProcessInputAsync_ReturnsFailedOutcome_WhenBinderThrowsUnexpectedException()
    {
        var fixture = CreateFixture();
        fixture.PrerequisiteChecker.Result = new CommandEligibilityResult(true, CommandExecutionStatus.Fulfilled, CommandExecutionReason.None);
        fixture.Binder.ExceptionToThrow = new InvalidOperationException("Binder exploded");

        var outcome = await fixture.Service.ProcessInputAsync(CreateMessage("!request song", messageId: "reply-id"));

        Assert.Equal(CommandExecutionStatus.Failed, outcome.Status);
        Assert.Equal(CommandExecutionReason.UnexpectedException, outcome.Reason);
        Assert.Equal("Command execution failed unexpectedly.", outcome.Message);
        Assert.IsType<InvalidOperationException>(outcome.Exception);
        Assert.Empty(fixture.Chat.Messages);
    }

    [Fact]
    public async Task ProcessInputAsync_ReturnsFailedOutcome_WhenHandlerThrowsAfterSuccessfulBinding()
    {
        var fixture = CreateFixture(commandName: nameof(ThrowingCommand), registryEntryFactory: CreateThrowingRegistryEntry);
        fixture.PrerequisiteChecker.Result = new CommandEligibilityResult(true, CommandExecutionStatus.Fulfilled, CommandExecutionReason.None);
        fixture.Binder.ResultFactory = _ => throw new InvalidOperationException("Handler exploded");

        var outcome = await fixture.Service.ProcessInputAsync(CreateMessage("!request song", messageId: "reply-id"));

        Assert.Equal(CommandExecutionStatus.Failed, outcome.Status);
        Assert.Equal(CommandExecutionReason.UnexpectedException, outcome.Reason);
        Assert.Equal("Command execution failed unexpectedly.", outcome.Message);
        Assert.IsType<InvalidOperationException>(outcome.Exception);
        Assert.Empty(fixture.Chat.Messages);
    }

    [Fact]
    public async Task ProcessInputAsync_DoesNotSendChatMessage_WhenFulfilledCommandHasNoConfiguredResponse()
    {
        var fixture = CreateFixture();
        fixture.PrerequisiteChecker.Result = new CommandEligibilityResult(true, CommandExecutionStatus.Fulfilled, CommandExecutionReason.None);
        fixture.Binder.ResultFactory = _ => CommandResult.Fulfilled();

        var outcome = await fixture.Service.ProcessInputAsync(CreateMessage("!request song", messageId: "reply-id"));

        Assert.Equal(CommandExecutionStatus.Fulfilled, outcome.Status);
        Assert.Empty(fixture.Chat.Messages);
    }

    [Fact]
    public async Task ProcessInputAsync_SendsFormattedReply_WhenFulfilledCommandProvidesTypedResultData()
    {
        var fixture = CreateFixture(commandConfiguration: CreateCommandConfiguration(response: "{username} requested {title} for {bits} bits"));
        fixture.PrerequisiteChecker.Result = new CommandEligibilityResult(true, CommandExecutionStatus.Fulfilled, CommandExecutionReason.None);
        fixture.Binder.ResultFactory = context => CommandResult<TestResponseData>.Fulfilled(new TestResponseData
        {
            Title = "Test Song"
        });

        var outcome = await fixture.Service.ProcessInputAsync(CreateMessage("!request song", cheerBits: 200, messageId: "reply-id"));

        Assert.Equal(CommandExecutionStatus.Fulfilled, outcome.Status);
        Assert.Equal("Test Song", ((TestResponseData)outcome.Data!).Title);
        Assert.Equal("Test Song", outcome.Placeholders["{title}"]);
        Assert.Equal("200", outcome.Placeholders["{bits}"]);
        Assert.Equal("User requested Test Song for 200 bits", Assert.Single(fixture.Chat.Messages).Message);
        Assert.True(Assert.Single(fixture.Chat.Messages).AsReply);
    }

    [Fact]
    public async Task ProcessInputAsync_SendsNonReplyMessage_WhenCommandSucceededButSourceMessageCannotBeRepliedTo()
    {
        var fixture = CreateFixture(commandConfiguration: CreateCommandConfiguration(response: "Queued {title}"));
        fixture.PrerequisiteChecker.Result = new CommandEligibilityResult(true, CommandExecutionStatus.Fulfilled, CommandExecutionReason.None);
        fixture.Binder.ResultFactory = _ => CommandResult<TestResponseData>.Fulfilled(new TestResponseData
        {
            Title = "Test Song"
        });

        var outcome = await fixture.Service.ProcessInputAsync(CreateMessage("!request song"));

        Assert.Equal(CommandExecutionStatus.Fulfilled, outcome.Status);
        Assert.Equal("Queued Test Song", Assert.Single(fixture.Chat.Messages).Message);
        Assert.False(Assert.Single(fixture.Chat.Messages).AsReply);
    }

    [Fact]
    public async Task ProcessInputAsync_DoesNotSendChatMessage_WhenFormattedResponseBecomesWhitespace()
    {
        var fixture = CreateFixture(commandConfiguration: CreateCommandConfiguration(response: "   {title}   "));
        fixture.PrerequisiteChecker.Result = new CommandEligibilityResult(true, CommandExecutionStatus.Fulfilled, CommandExecutionReason.None);
        fixture.Binder.ResultFactory = _ => CommandResult<TestResponseData>.Fulfilled(new TestResponseData
        {
            Title = null!
        });

        var outcome = await fixture.Service.ProcessInputAsync(CreateMessage("!request song", messageId: "reply-id"));

        Assert.Equal(CommandExecutionStatus.Fulfilled, outcome.Status);
        Assert.Empty(fixture.Chat.Messages);
    }

    [Fact]
    public async Task ProcessRewardInputAsync_UsesRewardInvocationKind_WhenRewardBindingMatches()
    {
        var fixture = CreateFixture(CreateCommandConfiguration(
            rewardTriggerMode: RewardTriggerMode.Existing,
            rewardBinding: new CommandRewardBindingSnapshot { RewardId = "reward-1" }));
        fixture.PrerequisiteChecker.Result = new CommandEligibilityResult(true, CommandExecutionStatus.Fulfilled, CommandExecutionReason.None);
        fixture.Binder.ResultFactory = context => CommandResult.Fulfilled();

        var outcome = await fixture.Service.ProcessRewardInputAsync(
            "reward-1",
            "broadcaster-id",
            "Broadcaster",
            "broadcaster",
            "user-id",
            "User",
            "user-login");

        Assert.Equal(CommandInvocationKind.Reward, outcome.InvocationKind);
        Assert.Equal(string.Empty, fixture.Binder.LastContext!.RawInputText);
        Assert.Equal("reward-1", fixture.Binder.LastContext.RewardId);
        Assert.Equal(CommandInvocationKind.Reward, fixture.PrerequisiteChecker.LastInvocationKind);
    }

    [Fact]
    public async Task ProcessRewardInputAsync_ReturnsCooldownBlockedOutcome_WhenRewardCommandIsCoolingDown()
    {
        var fixture = CreateFixture(CreateCommandConfiguration(
            rewardTriggerMode: RewardTriggerMode.Existing,
            rewardBinding: new CommandRewardBindingSnapshot { RewardId = "reward-1" }));
        fixture.PrerequisiteChecker.Result = new CommandEligibilityResult(true, CommandExecutionStatus.Fulfilled, CommandExecutionReason.None);
        fixture.CooldownManager.CanStart = false;
        fixture.CooldownManager.WaitSeconds = 12;

        var outcome = await fixture.Service.ProcessRewardInputAsync(
            "reward-1",
            "broadcaster-id",
            "Broadcaster",
            "broadcaster",
            "user-id",
            "User",
            "user-login");

        Assert.Equal(CommandInvocationKind.Reward, outcome.InvocationKind);
        Assert.Equal(CommandExecutionStatus.Blocked, outcome.Status);
        Assert.Equal(CommandExecutionReason.CooldownActive, outcome.Reason);
        Assert.Equal(12, outcome.CooldownRemainingSeconds);
        Assert.Equal("Please wait 12s…", outcome.Message);
    }

    [Fact]
    public async Task ProcessRewardInputAsync_SkipsExecution_WhenRewardIdDoesNotMatchAnyConfiguredCommand()
    {
        var fixture = CreateFixture(CreateCommandConfiguration(
            rewardTriggerMode: RewardTriggerMode.Existing,
            rewardBinding: new CommandRewardBindingSnapshot { RewardId = "reward-1" }));

        var outcome = await fixture.Service.ProcessRewardInputAsync(
            "different-reward",
            "broadcaster-id",
            "Broadcaster",
            "broadcaster",
            "user-id",
            "User",
            "user-login");

        Assert.Equal(CommandExecutionStatus.Skipped, outcome.Status);
        Assert.Equal(CommandExecutionReason.TriggerNotMatched, outcome.Reason);
        Assert.Equal(CommandInvocationKind.Chat, outcome.InvocationKind);
        Assert.Null(fixture.Binder.LastContext);
        Assert.Empty(fixture.Chat.Messages);
    }

    [Fact]
    public async Task ProcessInputAsync_DoesNotResolveBitsCommand_WhenPrefixDoesNotMatch()
    {
        var fixture = CreateFixture();

        var outcome = await fixture.Service.ProcessInputAsync(CreateMessage("?request song", cheerBits: 250));

        Assert.Equal(CommandExecutionStatus.Skipped, outcome.Status);
        Assert.Equal(CommandExecutionReason.TriggerNotMatched, outcome.Reason);
        Assert.Null(fixture.PrerequisiteChecker.LastBits);
    }

    private static CommandServiceFixture CreateFixture(
        CommandConfigurationSnapshot? commandConfiguration = null,
        string commandName = TestCommandName,
        Func<CommandRegistryEntry>? registryEntryFactory = null,
        bool registryInitialized = true,
        IDictionary<string, CommandConfigurationSnapshot>? commandConfigurations = null)
    {
        var settings = SettingsServiceTestSupport.CreateWithThrowingDb(
            twitchSettings: new TwitchSettingsSnapshot(true, "Connected!", "!", null, null),
            commandConfigurations: commandConfigurations is null
                ? new Dictionary<string, CommandConfigurationSnapshot>(StringComparer.Ordinal)
                {
                    [commandName] = (commandConfiguration ?? CreateCommandConfiguration()).Clone()
                }
                : new Dictionary<string, CommandConfigurationSnapshot>(commandConfigurations, StringComparer.Ordinal));

        var registry = new CommandRegistry(settings, new ReflectionPlaceholderProvider(), NullLogger<CommandRegistry>.Instance);
        SettingsServiceTestSupport.SetPrivateField(registry, "_entriesByName", new Dictionary<string, CommandRegistryEntry>(StringComparer.Ordinal)
        {
            [commandName] = (registryEntryFactory ?? CreateRegistryEntry)()
        });
        SettingsServiceTestSupport.SetPrivateField(registry, "_commandNamesByTrigger", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["request"] = commandName
        });
        SettingsServiceTestSupport.SetAutoProperty(registry, "IsInitialized", registryInitialized);

        var chat = new FakeTwitchChatService();
        var prerequisiteChecker = new FakePrerequisiteChecker();
        var cooldownManager = new FakeCooldownManager();
        var binder = new FakeArgumentBinder();
        var services = new ServiceCollection().BuildServiceProvider();

        var service = new CommandService(
            settings,
            registry,
            services,
            chat,
            new ArgumentParser(),
            prerequisiteChecker,
            cooldownManager,
            binder,
            new ResponseFormatter(),
            NullLogger<CommandService>.Instance);

        return new CommandServiceFixture(service, prerequisiteChecker, cooldownManager, binder, chat);
    }

    private static CommandRegistryEntry CreateRegistryEntry()
    {
        var commandType = typeof(TestCommand);
        var handlerMethod = commandType.GetMethod(nameof(TestCommand.ExecuteAsync), BindingFlags.Instance | BindingFlags.Public)!;

        return new CommandRegistryEntry
        {
            Name = TestCommandName,
            DefaultTrigger = "request",
            Attribute = new CommandAttribute("Test command"),
            RequiresInput = true,
            ImplementationType = commandType,
            HandlerMethod = handlerMethod,
            Placeholders = new Dictionary<string, string>()
        };
    }

    private static CommandRegistryEntry CreateThrowingRegistryEntry()
    {
        var commandType = typeof(ThrowingCommand);
        var handlerMethod = commandType.GetMethod(nameof(ThrowingCommand.ExecuteAsync), BindingFlags.Instance | BindingFlags.Public)!;

        return new CommandRegistryEntry
        {
            Name = nameof(ThrowingCommand),
            DefaultTrigger = "request",
            Attribute = new CommandAttribute("Throwing command"),
            RequiresInput = true,
            ImplementationType = commandType,
            HandlerMethod = handlerMethod,
            Placeholders = new Dictionary<string, string>()
        };
    }

    private static CommandConfigurationSnapshot CreateCommandConfiguration(
        string? response = null,
        string? accessDeniedResponse = null,
        RewardTriggerMode rewardTriggerMode = RewardTriggerMode.Disabled,
        CommandRewardBindingSnapshot? rewardBinding = null) =>
        new()
        {
            Trigger = "request",
            ChatTriggerMode = ChatTriggerMode.Chat,
            RewardTriggerMode = rewardTriggerMode,
            RequiredAccessLevel = CommandAccessLevel.Everyone,
            CooldownScope = CommandCooldownScope.Global,
            BitsThreshold = 0,
            RewardBinding = rewardBinding,
            Cooldown = 0,
            Response = response,
            AccessDeniedResponse = accessDeniedResponse
        };

    private static ChannelChatMessage CreateMessage(string text, int? cheerBits = null, int[]? fragmentBits = null, string? messageId = null)
    {
        var fragments = new List<ChatMessageFragment>
        {
            new() { Type = "text", Text = text }
        };

        if (fragmentBits is not null)
        {
            fragments.AddRange(fragmentBits.Select(bits => new ChatMessageFragment
            {
                Type = "cheermote",
                Text = $"cheer{bits}",
                Cheermote = new()
                {
                    Prefix = "cheer",
                    Bits = bits,
                    Tier = 1
                }
            }));
        }

        return new ChannelChatMessage
        {
            BroadcasterUserId = "broadcaster-id",
            BroadcasterUserLogin = "broadcaster",
            BroadcasterUserName = "Broadcaster",
            ChatterUserId = "user-id",
            ChatterUserLogin = "user-login",
            ChatterUserName = "User",
            MessageId = messageId ?? string.Empty,
            Cheer = cheerBits is null ? null : new() { Bits = cheerBits.Value },
            Message = new ChatMessage
            {
                Text = text,
                Fragments = [..fragments]
            }
        };
    }

    private sealed class FakePrerequisiteChecker : IPrerequisiteChecker
    {
        public CommandEligibilityResult Result { get; set; } = new(true, CommandExecutionStatus.Fulfilled, CommandExecutionReason.None);
        public int? LastBits { get; private set; }
        public CommandInvocationKind? LastInvocationKind { get; private set; }

        public CommandEligibilityResult Evaluate(ChannelChatMessage msg, CommandConfigurationSnapshot settings, int bits, CommandInvocationKind invocationKind)
        {
            LastBits = bits;
            LastInvocationKind = invocationKind;
            return Result;
        }
    }

    private sealed class FakeCooldownManager : ICooldownManager
    {
        public bool CanStart { get; set; } = true;
        public int WaitSeconds { get; set; }

        public bool TryStart(string commandKey, string executorUserId, CommandCooldownScope cooldownScope, uint secs, out int wait)
        {
            wait = WaitSeconds;
            return CanStart;
        }
    }

    private sealed class FakeArgumentBinder : IArgumentBinder
    {
        public CommandContext? LastContext { get; private set; }
        public Func<CommandContext, ICommandResult> ResultFactory { get; set; } = _ => CommandResult.Fulfilled();
        public Exception? ExceptionToThrow { get; set; }

        public Task<BoundCommandResult> BindAndInvokeAsync(object handler, MethodInfo method, CommandContext context, IReadOnlyList<string> tokens)
        {
            LastContext = context;
            if (ExceptionToThrow is not null)
                throw ExceptionToThrow;

            return Task.FromResult(new BoundCommandResult(ResultFactory(context), new Dictionary<string, string>
            {
                ["{username}"] = context.ExecutorDisplayName,
                ["{bits}"] = context.Bits.ToString()
            }));
        }
    }

    private sealed class FakeTwitchChatService : ITwitchChatService
    {
        public List<(string Message, bool AsReply)> Messages { get; } = [];

        public Task ConnectAsync(string username, string accessToken, string channelLogin) => Task.CompletedTask;
        public Task DisconnectAsync() => Task.CompletedTask;

        public void SendMessage(ChannelChatMessage senderMessage, string message, bool asReply = true)
        {
            Messages.Add((message, asReply));
        }
    }

    private sealed class CommandServiceFixture(
        CommandService service,
        FakePrerequisiteChecker prerequisiteChecker,
        FakeCooldownManager cooldownManager,
        FakeArgumentBinder binder,
        FakeTwitchChatService chat)
    {
        public CommandService Service { get; } = service;
        public FakePrerequisiteChecker PrerequisiteChecker { get; } = prerequisiteChecker;
        public FakeCooldownManager CooldownManager { get; } = cooldownManager;
        public FakeArgumentBinder Binder { get; } = binder;
        public FakeTwitchChatService Chat { get; } = chat;
    }

    private const string TestCommandName = nameof(TestCommand);

    private sealed class TestCommand
    {
        [CommandExecution]
        public Task<CommandResult> ExecuteAsync(CommandContext context, string query) =>
            Task.FromResult(CommandResult.Fulfilled());
    }

    private sealed class ThrowingCommand
    {
        [CommandExecution]
        public Task<CommandResult> ExecuteAsync(CommandContext context, string query) =>
            throw new InvalidOperationException("This should be replaced by the binder test result.");
    }

    private sealed class TestResponseData
    {
        public string Title { get; init; } = string.Empty;
    }
}
