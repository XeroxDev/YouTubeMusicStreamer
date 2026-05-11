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

using TwitchLib.EventSub.Core.Models.Chat;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;
using YouTubeMusicStreamer.Services.App.Persistence;
using YouTubeMusicStreamer.Services.Commands;
using YouTubeMusicStreamer.Services.Commands.PrerequisiteChecking;

namespace YouTubeMusicStreamer.Tests.Services.Commands.PrerequisiteChecking;

public class PrerequisiteCheckerTests
{
    private readonly PrerequisiteChecker _checker = new();

    [Fact]
    public void Evaluate_ReturnsSkipped_WhenRewardTriggerIsDisabled()
    {
        var result = _checker.Evaluate(
            CreateMessage(rewardId: "reward-1"),
            CreateSettings(rewardTriggerMode: RewardTriggerMode.Disabled),
            bits: 0,
            CommandInvocationKind.Reward);

        Assert.False(result.CanExecute);
        Assert.Equal(CommandExecutionStatus.Skipped, result.Status);
        Assert.Equal(CommandExecutionReason.CommandDisabled, result.Reason);
    }

    [Fact]
    public void Evaluate_ReturnsBlocked_WhenRewardBindingIsMissing()
    {
        var result = _checker.Evaluate(
            CreateMessage(rewardId: "reward-1"),
            CreateSettings(rewardTriggerMode: RewardTriggerMode.Existing),
            bits: 0,
            CommandInvocationKind.Reward);

        Assert.False(result.CanExecute);
        Assert.Equal(CommandExecutionStatus.Blocked, result.Status);
        Assert.Equal(CommandExecutionReason.RewardNotConfigured, result.Reason);
        Assert.Equal("No Twitch reward is configured for this command.", result.Message);
    }

    [Fact]
    public void Evaluate_ReturnsSkipped_WhenRewardIdDoesNotMatch()
    {
        var result = _checker.Evaluate(
            CreateMessage(rewardId: "reward-live"),
            CreateSettings(
                rewardTriggerMode: RewardTriggerMode.Existing,
                rewardBinding: new CommandRewardBindingSnapshot { RewardId = "reward-configured" }),
            bits: 0,
            CommandInvocationKind.Reward);

        Assert.False(result.CanExecute);
        Assert.Equal(CommandExecutionStatus.Skipped, result.Status);
        Assert.Equal(CommandExecutionReason.RewardMismatch, result.Reason);
    }

    [Fact]
    public void Evaluate_ReturnsBlocked_WhenBitsOnlyModeHasNoThreshold()
    {
        var result = _checker.Evaluate(
            CreateMessage(),
            CreateSettings(chatTriggerMode: ChatTriggerMode.BitsOnly, bitsThreshold: 0),
            bits: 0,
            CommandInvocationKind.Bits);

        Assert.False(result.CanExecute);
        Assert.Equal(CommandExecutionStatus.Blocked, result.Status);
        Assert.Equal(CommandExecutionReason.InvalidConfiguration, result.Reason);
        Assert.Equal("Bits-only mode requires a positive bits threshold.", result.Message);
    }

    [Fact]
    public void Evaluate_ReturnsFulfilled_WhenChatModeIsEnabledAndAccessPasses()
    {
        var result = _checker.Evaluate(
            CreateMessage(),
            CreateSettings(chatTriggerMode: ChatTriggerMode.Chat),
            bits: 0,
            CommandInvocationKind.Chat);

        Assert.True(result.CanExecute);
        Assert.Equal(CommandExecutionStatus.Fulfilled, result.Status);
        Assert.Equal(CommandExecutionReason.None, result.Reason);
    }

    [Fact]
    public void Evaluate_ReturnsSkipped_WhenChatTriggerModeIsDisabled()
    {
        var result = _checker.Evaluate(
            CreateMessage(),
            CreateSettings(chatTriggerMode: ChatTriggerMode.Disabled),
            bits: 0,
            CommandInvocationKind.Chat);

        Assert.False(result.CanExecute);
        Assert.Equal(CommandExecutionStatus.Skipped, result.Status);
        Assert.Equal(CommandExecutionReason.CommandDisabled, result.Reason);
    }

    [Fact]
    public void Evaluate_ReturnsBlocked_WhenBitsThresholdIsNotMet()
    {
        var result = _checker.Evaluate(
            CreateMessage(),
            CreateSettings(chatTriggerMode: ChatTriggerMode.BitsOnly, bitsThreshold: 250),
            bits: 100,
            CommandInvocationKind.Bits);

        Assert.False(result.CanExecute);
        Assert.Equal(CommandExecutionStatus.Blocked, result.Status);
        Assert.Equal(CommandExecutionReason.BitsThresholdNotMet, result.Reason);
        Assert.Equal("This command requires at least 250 bits.", result.Message);
    }

    [Fact]
    public void Evaluate_ReturnsFulfilled_WhenBitsThresholdIsMet()
    {
        var result = _checker.Evaluate(
            CreateMessage(),
            CreateSettings(chatTriggerMode: ChatTriggerMode.BitsOnly, bitsThreshold: 250),
            bits: 250,
            CommandInvocationKind.Bits);

        Assert.True(result.CanExecute);
        Assert.Equal(CommandExecutionStatus.Fulfilled, result.Status);
        Assert.Equal(CommandExecutionReason.None, result.Reason);
    }

    [Fact]
    public void Evaluate_ReturnsFulfilled_WhenRewardIdMatchesConfiguredBinding()
    {
        var result = _checker.Evaluate(
            CreateMessage(rewardId: "reward-1"),
            CreateSettings(
                rewardTriggerMode: RewardTriggerMode.Existing,
                rewardBinding: new CommandRewardBindingSnapshot { RewardId = "reward-1" }),
            bits: 0,
            CommandInvocationKind.Reward);

        Assert.True(result.CanExecute);
        Assert.Equal(CommandExecutionStatus.Fulfilled, result.Status);
        Assert.Equal(CommandExecutionReason.None, result.Reason);
    }

    [Fact]
    public void Evaluate_AllowsRewardInvocations_EvenWhenAccessLevelWouldBlockChat()
    {
        var result = _checker.Evaluate(
            CreateMessage(rewardId: "reward-1"),
            CreateSettings(
                requiredAccessLevel: CommandAccessLevel.Broadcaster,
                rewardTriggerMode: RewardTriggerMode.Existing,
                rewardBinding: new CommandRewardBindingSnapshot { RewardId = "reward-1" }),
            bits: 0,
            CommandInvocationKind.Reward);

        Assert.True(result.CanExecute);
        Assert.Equal(CommandExecutionStatus.Fulfilled, result.Status);
        Assert.Equal(CommandExecutionReason.None, result.Reason);
    }

    [Fact]
    public void Evaluate_ReturnsSkipped_WhenRewardModeIsDisabled_EvenForBroadcaster()
    {
        var result = _checker.Evaluate(
            CreateMessage(rewardId: "reward-1", chatterUserId: "broadcaster-1"),
            CreateSettings(
                rewardTriggerMode: RewardTriggerMode.Disabled,
                requiredAccessLevel: CommandAccessLevel.Broadcaster,
                rewardBinding: new CommandRewardBindingSnapshot { RewardId = "reward-1" }),
            bits: 0,
            CommandInvocationKind.Reward);

        Assert.False(result.CanExecute);
        Assert.Equal(CommandExecutionStatus.Skipped, result.Status);
        Assert.Equal(CommandExecutionReason.CommandDisabled, result.Reason);
    }

    [Fact]
    public void Evaluate_ReturnsAccessDenied_WhenUserDoesNotMeetRequiredAccess()
    {
        var result = _checker.Evaluate(
            CreateMessage(),
            CreateSettings(requiredAccessLevel: CommandAccessLevel.Moderator),
            bits: 0,
            CommandInvocationKind.Chat);

        Assert.False(result.CanExecute);
        Assert.Equal(CommandExecutionStatus.Blocked, result.Status);
        Assert.Equal(CommandExecutionReason.AccessDenied, result.Reason);
        Assert.Equal("This command requires moderator access.", result.Message);
    }

    [Fact]
    public void Evaluate_ReturnsAccessDenied_WhenSubscriberAccessIsRequiredButViewerHasNoBadge()
    {
        var result = _checker.Evaluate(
            CreateMessage(),
            CreateSettings(requiredAccessLevel: CommandAccessLevel.Subscriber),
            bits: 0,
            CommandInvocationKind.Chat);

        Assert.False(result.CanExecute);
        Assert.Equal(CommandExecutionStatus.Blocked, result.Status);
        Assert.Equal(CommandExecutionReason.AccessDenied, result.Reason);
        Assert.Equal("This command requires subscriber access.", result.Message);
    }

    [Fact]
    public void Evaluate_ReturnsFulfilled_WhenSubscriberAccessIsRequiredAndUserIsSubscriber()
    {
        var result = _checker.Evaluate(
            CreateMessage(badgeSetIds: ["subscriber"]),
            CreateSettings(requiredAccessLevel: CommandAccessLevel.Subscriber),
            bits: 0,
            CommandInvocationKind.Chat);

        Assert.True(result.CanExecute);
        Assert.Equal(CommandExecutionStatus.Fulfilled, result.Status);
    }

    [Fact]
    public void Evaluate_ReturnsAccessDenied_WhenVipAccessIsRequiredButUserIsOnlySubscriber()
    {
        var result = _checker.Evaluate(
            CreateMessage(badgeSetIds: ["subscriber"]),
            CreateSettings(requiredAccessLevel: CommandAccessLevel.Vip),
            bits: 0,
            CommandInvocationKind.Chat);

        Assert.False(result.CanExecute);
        Assert.Equal(CommandExecutionStatus.Blocked, result.Status);
        Assert.Equal(CommandExecutionReason.AccessDenied, result.Reason);
        Assert.Equal("This command requires VIP access.", result.Message);
    }

    [Theory]
    [InlineData(CommandAccessLevel.Subscriber)]
    [InlineData(CommandAccessLevel.Vip)]
    [InlineData(CommandAccessLevel.Moderator)]
    [InlineData(CommandAccessLevel.Broadcaster)]
    public void Evaluate_ReturnsFulfilled_WhenBroadcasterInvokesRestrictedCommand(CommandAccessLevel accessLevel)
    {
        var result = _checker.Evaluate(
            CreateMessage(chatterUserId: "broadcaster-1"),
            CreateSettings(requiredAccessLevel: accessLevel),
            bits: 0,
            CommandInvocationKind.Chat);

        Assert.True(result.CanExecute);
        Assert.Equal(CommandExecutionStatus.Fulfilled, result.Status);
        Assert.Equal(CommandExecutionReason.None, result.Reason);
    }

    [Fact]
    public void Evaluate_AllowsVipProtectedCommand_ForModerators()
    {
        var result = _checker.Evaluate(
            CreateMessage(badgeSetIds: ["moderator"]),
            CreateSettings(requiredAccessLevel: CommandAccessLevel.Vip),
            bits: 0,
            CommandInvocationKind.Chat);

        Assert.True(result.CanExecute);
        Assert.Equal(CommandExecutionStatus.Fulfilled, result.Status);
        Assert.Equal(CommandExecutionReason.None, result.Reason);
    }

    [Fact]
    public void Evaluate_ReturnsInvalidConfiguration_ForUnexpectedChatMode()
    {
        var result = _checker.Evaluate(
            CreateMessage(),
            CreateSettings(chatTriggerMode: ChatTriggerMode.ChatAndBits),
            bits: 0,
            CommandInvocationKind.Chat);

        Assert.False(result.CanExecute);
        Assert.Equal(CommandExecutionStatus.Blocked, result.Status);
        Assert.Equal(CommandExecutionReason.InvalidConfiguration, result.Reason);
    }

    private static ChannelChatMessage CreateMessage(
        string? rewardId = null,
        string[]? badgeSetIds = null,
        string chatterUserId = "viewer-1",
        string broadcasterUserId = "broadcaster-1")
    {
        var message = new ChannelChatMessage
        {
            ChatterUserId = chatterUserId,
            BroadcasterUserId = broadcasterUserId,
            Badges = badgeSetIds?.Select(setId => new ChatBadge
            {
                SetId = setId,
                Id = "1",
                Info = string.Empty
            }).ToArray() ?? []
        };

        if (rewardId is not null)
            message.ChannelPointsCustomRewardId = rewardId;

        return message;
    }
    private static CommandConfigurationSnapshot CreateSettings(
        ChatTriggerMode chatTriggerMode = ChatTriggerMode.Chat,
        RewardTriggerMode rewardTriggerMode = RewardTriggerMode.Disabled,
        CommandAccessLevel requiredAccessLevel = CommandAccessLevel.Everyone,
        uint bitsThreshold = 0,
        CommandRewardBindingSnapshot? rewardBinding = null) =>
        new()
        {
            Trigger = "test",
            ChatTriggerMode = chatTriggerMode,
            RewardTriggerMode = rewardTriggerMode,
            RequiredAccessLevel = requiredAccessLevel,
            CooldownScope = CommandCooldownScope.Global,
            BitsThreshold = bitsThreshold,
            RewardBinding = rewardBinding,
            Cooldown = 0
        };
}
