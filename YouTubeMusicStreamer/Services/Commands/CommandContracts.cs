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
using YouTubeMusicStreamer.Attributes;
using YouTubeMusicStreamer.Services.App.Persistence;
using YouTubeMusicStreamer.Services.Twitch.Interfaces;

namespace YouTubeMusicStreamer.Services.Commands;

public enum CommandExecutionStatus
{
    Fulfilled,
    Blocked,
    BadInput,
    Skipped,
    Failed
}

public enum CommandExecutionReason
{
    None,
    NotInitialized,
    CommandNotFound,
    TriggerNotMatched,
    CommandDisabled,
    RewardNotConfigured,
    RewardMismatch,
    AccessDenied,
    BitsThresholdNotMet,
    CooldownActive,
    MissingArgument,
    InvalidArgument,
    MissingIntegration,
    InvalidConfiguration,
    InvalidSignature,
    UnexpectedException
}

public enum CommandInvocationKind
{
    Chat,
    Bits,
    Reward
}

public sealed class CommandExecutionOutcome
{
    public required string CommandName { get; init; }
    public required string Trigger { get; init; }
    public required CommandInvocationKind InvocationKind { get; init; }
    public required CommandExecutionStatus Status { get; init; }
    public CommandExecutionReason Reason { get; init; } = CommandExecutionReason.None;
    public string? Message { get; init; }
    public object? Data { get; init; }
    public IReadOnlyDictionary<string, string> Placeholders { get; init; } = new Dictionary<string, string>();
    public int? CooldownRemainingSeconds { get; init; }
    public Exception? Exception { get; init; }

    public bool IsFulfilled => Status == CommandExecutionStatus.Fulfilled;
}

public interface ICommandResult
{
    CommandExecutionStatus Status { get; }
    CommandExecutionReason Reason { get; }
    string? Message { get; }
    object? Data { get; }
}

public class CommandResult : ICommandResult
{
    public required CommandExecutionStatus Status { get; init; }
    public CommandExecutionReason Reason { get; init; } = CommandExecutionReason.None;
    public string? Message { get; init; }
    public virtual object? Data => null;

    public static CommandResult Fulfilled(string? message = null) =>
        new()
        {
            Status = CommandExecutionStatus.Fulfilled,
            Message = message
        };

    public static CommandResult Blocked(CommandExecutionReason reason, string? message = null) =>
        new()
        {
            Status = CommandExecutionStatus.Blocked,
            Reason = reason,
            Message = message
        };

    public static CommandResult BadInput(CommandExecutionReason reason, string? message = null) =>
        new()
        {
            Status = CommandExecutionStatus.BadInput,
            Reason = reason,
            Message = message
        };

    public static CommandResult Skipped(CommandExecutionReason reason, string? message = null) =>
        new()
        {
            Status = CommandExecutionStatus.Skipped,
            Reason = reason,
            Message = message
        };
}

public sealed class CommandResult<TData> : ICommandResult
{
    public required CommandExecutionStatus Status { get; init; }
    public CommandExecutionReason Reason { get; init; } = CommandExecutionReason.None;
    public string? Message { get; init; }
    public TData? Value { get; init; }
    public object? Data => Value;

    public static CommandResult<TData> Fulfilled(TData? value = default, string? message = null) =>
        new()
        {
            Status = CommandExecutionStatus.Fulfilled,
            Value = value,
            Message = message
        };

    public static CommandResult<TData> Blocked(CommandExecutionReason reason, string? message = null) =>
        new()
        {
            Status = CommandExecutionStatus.Blocked,
            Reason = reason,
            Message = message
        };

    public static CommandResult<TData> BadInput(CommandExecutionReason reason, string? message = null) =>
        new()
        {
            Status = CommandExecutionStatus.BadInput,
            Reason = reason,
            Message = message
        };

    public static CommandResult<TData> Skipped(CommandExecutionReason reason, string? message = null) =>
        new()
        {
            Status = CommandExecutionStatus.Skipped,
            Reason = reason,
            Message = message
        };
}

public sealed class CommandContext
{
    private readonly ITwitchChatService _twitchChatService;

    internal CommandContext(
        CommandInvocationKind invocationKind,
        string commandName,
        string trigger,
        CommandConfigurationSnapshot configuration,
        ChannelChatMessage sourceMessage,
        string rawInputText,
        int bits,
        string? rewardId,
        CancellationToken cancellationToken,
        ITwitchChatService twitchChatService)
    {
        InvocationKind = invocationKind;
        CommandName = commandName;
        Trigger = trigger;
        Configuration = configuration;
        SourceMessage = sourceMessage;
        RawInputText = rawInputText;
        Bits = bits;
        RewardId = rewardId;
        CancellationToken = cancellationToken;
        _twitchChatService = twitchChatService;
    }

    public CommandInvocationKind InvocationKind { get; }
    public string CommandName { get; }
    public string Trigger { get; }
    public CommandConfigurationSnapshot Configuration { get; }
    public ChannelChatMessage SourceMessage { get; }
    public string RawInputText { get; }
    public int Bits { get; }
    public string? RewardId { get; }
    public CancellationToken CancellationToken { get; }

    public string ExecutorUserId => SourceMessage.ChatterUserId;
    public string ExecutorLogin => SourceMessage.ChatterUserLogin;
    public string ExecutorDisplayName => SourceMessage.ChatterUserName;
    public string BroadcasterUserId => SourceMessage.BroadcasterUserId;
    public string BroadcasterLogin => SourceMessage.BroadcasterUserLogin;
    public string BroadcasterDisplayName => SourceMessage.BroadcasterUserName;
    public bool CanReply => !string.IsNullOrWhiteSpace(SourceMessage.MessageId);
    public bool IsSubscriber => SourceMessage.IsSubscriber;
    public bool IsModerator => SourceMessage.IsModerator;
    public bool IsVip => SourceMessage.IsVip;
    public bool IsBroadcaster => string.Equals(ExecutorUserId, BroadcasterUserId, StringComparison.Ordinal);
    public IReadOnlyList<ChatBadge> Badges => SourceMessage.Badges ?? [];

    public string GetInvocationDisplay(string commandPrefix) => InvocationKind switch
    {
        CommandInvocationKind.Chat => $"{commandPrefix}{Trigger}",
        CommandInvocationKind.Bits => $"{commandPrefix}{Trigger} ({Configuration.BitsThreshold} bits)",
        CommandInvocationKind.Reward => $"reward \"{Configuration.RewardBinding?.ManagedRewardName ?? Configuration.RewardBinding?.RewardId ?? "linked reward"}\"",
        _ => Trigger
    };

    public Task ReplyAsync(string message)
    {
        _twitchChatService.SendMessage(SourceMessage, message, asReply: CanReply);
        return Task.CompletedTask;
    }

    public Task SendAsync(string message)
    {
        _twitchChatService.SendMessage(SourceMessage, message, asReply: false);
        return Task.CompletedTask;
    }
}

public sealed record CommandInvocationDescriptor(
    CommandInvocationKind Kind,
    string DisplayText);

public sealed class CommandDescriptor
{
    public required string Name { get; init; }
    public required CommandAttribute Attribute { get; init; }
    public required bool RequiresInput { get; init; }
    public required CommandConfigurationSnapshot CommandConfiguration { get; init; }
    public required IReadOnlyDictionary<string, string> Placeholders { get; init; }
    public required IReadOnlyList<CommandInvocationDescriptor> Invocations { get; init; }
}

public sealed class CommandRegistryEntry
{
    public required string Name { get; init; }
    public required string DefaultTrigger { get; init; }
    public required CommandAttribute Attribute { get; init; }
    public required bool RequiresInput { get; init; }
    public required Type ImplementationType { get; init; }
    public required MethodInfo HandlerMethod { get; init; }
    public required IReadOnlyDictionary<string, string> Placeholders { get; init; }
}

public sealed record CommandEligibilityResult(
    bool CanExecute,
    CommandExecutionStatus Status,
    CommandExecutionReason Reason,
    string? Message = null);
