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
using TwitchLib.EventSub.Core.Models.Chat;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;
using YouTubeMusicStreamer.Services.App.Persistence;
using YouTubeMusicStreamer.Services.Commands;
using YouTubeMusicStreamer.Services.Twitch.Interfaces;

namespace YouTubeMusicStreamer.Tests.Services.Commands;

public sealed class CommandContractsTests
{
    [Fact]
    public void CommandResultFactories_AssignExpectedStatusReasonAndPayload()
    {
        var fulfilled = CommandResult<string>.Fulfilled("song", "Queued");
        var blocked = CommandResult.Blocked(CommandExecutionReason.CooldownActive, "Wait");
        var badInput = CommandResult.BadInput(CommandExecutionReason.InvalidArgument, "Bad");
        var skipped = CommandResult<string>.Skipped(CommandExecutionReason.TriggerNotMatched, "No match");

        Assert.Equal(CommandExecutionStatus.Fulfilled, fulfilled.Status);
        Assert.Equal("song", fulfilled.Value);
        Assert.Equal("song", fulfilled.Data);
        Assert.Equal("Queued", fulfilled.Message);

        Assert.Equal(CommandExecutionStatus.Blocked, blocked.Status);
        Assert.Equal(CommandExecutionReason.CooldownActive, blocked.Reason);
        Assert.Equal("Wait", blocked.Message);

        Assert.Equal(CommandExecutionStatus.BadInput, badInput.Status);
        Assert.Equal(CommandExecutionReason.InvalidArgument, badInput.Reason);
        Assert.Equal("Bad", badInput.Message);

        Assert.Equal(CommandExecutionStatus.Skipped, skipped.Status);
        Assert.Equal(CommandExecutionReason.TriggerNotMatched, skipped.Reason);
        Assert.Equal("No match", skipped.Message);
    }

    [Fact]
    public async Task CommandContext_UsesReplyOnlyWhenMessageCanActuallyBeRepliedTo()
    {
        var chat = new FakeTwitchChatService();
        var replyContext = CreateContext(chat, "reply-id");
        var noReplyContext = CreateContext(chat, string.Empty);

        await replyContext.ReplyAsync("reply");
        await noReplyContext.ReplyAsync("fallback");
        await noReplyContext.SendAsync("plain");

        Assert.Equal(
            [("reply", true), ("fallback", false), ("plain", false)],
            chat.Messages.Select(m => (m.Message, m.AsReply)).ToArray());
    }

    [Fact]
    public void CommandContext_ProducesSafeInvocationDisplayAcrossInvocationKinds()
    {
        var rewardBinding = new CommandRewardBindingSnapshot { RewardId = "reward-1", ManagedRewardName = "Play Song" };
        var bitsContext = CreateContext(new FakeTwitchChatService(), "reply-id", CommandInvocationKind.Bits, bits: 250, configuration: CreateConfiguration(bitsThreshold: 250));
        var rewardContext = CreateContext(new FakeTwitchChatService(), "reply-id", CommandInvocationKind.Reward, rewardId: "reward-1", configuration: CreateConfiguration(rewardBinding: rewardBinding));

        Assert.Equal("!request (250 bits)", bitsContext.GetInvocationDisplay("!"));
        Assert.Equal("reward \"Play Song\"", rewardContext.GetInvocationDisplay("!"));
    }

    [Fact]
    public void CommandExecutionOutcome_IsFulfilledReflectsOnlyFulfilledStatus()
    {
        var fulfilled = new CommandExecutionOutcome
        {
            CommandName = "RequestCommand",
            Trigger = "request",
            InvocationKind = CommandInvocationKind.Chat,
            Status = CommandExecutionStatus.Fulfilled
        };
        var blocked = new CommandExecutionOutcome
        {
            CommandName = "RequestCommand",
            Trigger = "request",
            InvocationKind = CommandInvocationKind.Chat,
            Status = CommandExecutionStatus.Blocked
        };

        Assert.True(fulfilled.IsFulfilled);
        Assert.False(blocked.IsFulfilled);
    }

    private static CommandContext CreateContext(
        FakeTwitchChatService chat,
        string messageId,
        CommandInvocationKind invocationKind = CommandInvocationKind.Chat,
        int bits = 0,
        string? rewardId = null,
        CommandConfigurationSnapshot? configuration = null)
    {
        return (CommandContext)Activator.CreateInstance(
            typeof(CommandContext),
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            args:
            [
                invocationKind,
                "RequestCommand",
                "request",
                configuration ?? CreateConfiguration(),
                new ChannelChatMessage
                {
                    BroadcasterUserId = "broadcaster-id",
                    BroadcasterUserLogin = "broadcaster",
                    BroadcasterUserName = "Broadcaster",
                    ChatterUserId = "user-id",
                    ChatterUserLogin = "user-login",
                    ChatterUserName = "User",
                    MessageId = messageId,
                    Badges = [],
                    Message = new ChatMessage { Text = "!request test", Fragments = [] }
                },
                "!request test",
                bits,
                rewardId,
                CancellationToken.None,
                chat
            ],
            culture: null)!;
    }

    private static CommandConfigurationSnapshot CreateConfiguration(int bitsThreshold = 0, CommandRewardBindingSnapshot? rewardBinding = null) =>
        new()
        {
            Trigger = "request",
            ChatTriggerMode = bitsThreshold > 0 ? ChatTriggerMode.BitsOnly : ChatTriggerMode.Chat,
            RewardTriggerMode = rewardBinding is null ? RewardTriggerMode.Disabled : RewardTriggerMode.Existing,
            RequiredAccessLevel = CommandAccessLevel.Everyone,
            CooldownScope = CommandCooldownScope.Global,
            BitsThreshold = (uint)bitsThreshold,
            RewardBinding = rewardBinding,
            Cooldown = 0,
            Response = null
        };

    private sealed class FakeTwitchChatService : ITwitchChatService
    {
        public List<(string Message, bool AsReply)> Messages { get; } = [];

        public Task ConnectAsync(string username, string accessToken, string channelLogin) => Task.CompletedTask;
        public Task DisconnectAsync() => Task.CompletedTask;

        public void SendMessage(ChannelChatMessage sourceMessage, string message, bool asReply = false)
        {
            Messages.Add((message, asReply));
        }
    }
}
