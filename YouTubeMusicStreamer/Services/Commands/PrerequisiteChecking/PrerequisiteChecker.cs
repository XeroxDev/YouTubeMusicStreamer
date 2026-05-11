// This file is part of YouTubeMusicStreamer.
// Copyright (C) 2025 Dominic Ris
// 
// YouTubeMusicStreamer is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as published
// by the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version (the "AGPLv3").
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

using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;
using YouTubeMusicStreamer.Services.App.Persistence;

namespace YouTubeMusicStreamer.Services.Commands.PrerequisiteChecking;

public class PrerequisiteChecker : IPrerequisiteChecker
{
    public CommandEligibilityResult Evaluate(ChannelChatMessage msg, CommandConfigurationSnapshot settings, int bits, CommandInvocationKind invocationKind)
    {
        if (!string.IsNullOrWhiteSpace(msg.ChannelPointsCustomRewardId))
        {
            if (settings.RewardTriggerMode == RewardTriggerMode.Disabled)
                return new CommandEligibilityResult(false, CommandExecutionStatus.Skipped, CommandExecutionReason.CommandDisabled);

            if (string.IsNullOrWhiteSpace(settings.RewardBinding?.RewardId))
                return new CommandEligibilityResult(false, CommandExecutionStatus.Blocked, CommandExecutionReason.RewardNotConfigured, "No Twitch reward is configured for this command.");

            return string.Equals(settings.RewardBinding.RewardId, msg.ChannelPointsCustomRewardId, StringComparison.Ordinal)
                ? new CommandEligibilityResult(true, CommandExecutionStatus.Fulfilled, CommandExecutionReason.None)
                : new CommandEligibilityResult(false, CommandExecutionStatus.Skipped, CommandExecutionReason.RewardMismatch);
        }

        var accessEligibility = EvaluateAccess(msg, settings.RequiredAccessLevel, invocationKind);
        if (!accessEligibility.CanExecute)
            return accessEligibility;

        return settings.ChatTriggerMode switch
        {
            ChatTriggerMode.Disabled => new CommandEligibilityResult(false, CommandExecutionStatus.Skipped, CommandExecutionReason.CommandDisabled),
            ChatTriggerMode.Chat => new CommandEligibilityResult(true, CommandExecutionStatus.Fulfilled, CommandExecutionReason.None),
            ChatTriggerMode.BitsOnly when settings.BitsThreshold == 0 =>
                new CommandEligibilityResult(false, CommandExecutionStatus.Blocked, CommandExecutionReason.InvalidConfiguration, "Bits-only mode requires a positive bits threshold."),
            ChatTriggerMode.BitsOnly when bits < settings.BitsThreshold =>
                new CommandEligibilityResult(false, CommandExecutionStatus.Blocked, CommandExecutionReason.BitsThresholdNotMet, $"This command requires at least {settings.BitsThreshold} bits."),
            ChatTriggerMode.BitsOnly => new CommandEligibilityResult(true, CommandExecutionStatus.Fulfilled, CommandExecutionReason.None),
            _ => new CommandEligibilityResult(false, CommandExecutionStatus.Blocked, CommandExecutionReason.InvalidConfiguration)
        };
    }

    private static CommandEligibilityResult EvaluateAccess(
        ChannelChatMessage msg,
        CommandAccessLevel requiredAccessLevel,
        CommandInvocationKind invocationKind)
    {
        if (requiredAccessLevel == CommandAccessLevel.Everyone)
            return new CommandEligibilityResult(true, CommandExecutionStatus.Fulfilled, CommandExecutionReason.None);

        if (invocationKind == CommandInvocationKind.Reward)
            return new CommandEligibilityResult(true, CommandExecutionStatus.Fulfilled, CommandExecutionReason.None);

        var hasAccess = requiredAccessLevel switch
        {
            CommandAccessLevel.Everyone => true,
            CommandAccessLevel.Subscriber => msg.IsSubscriber || msg.IsVip || msg.IsModerator || IsBroadcaster(msg),
            CommandAccessLevel.Vip => msg.IsVip || msg.IsModerator || IsBroadcaster(msg),
            CommandAccessLevel.Moderator => msg.IsModerator || IsBroadcaster(msg),
            CommandAccessLevel.Broadcaster => IsBroadcaster(msg),
            _ => false
        };

        return hasAccess
            ? new CommandEligibilityResult(true, CommandExecutionStatus.Fulfilled, CommandExecutionReason.None)
            : new CommandEligibilityResult(false, CommandExecutionStatus.Blocked, CommandExecutionReason.AccessDenied, $"This command requires {GetAccessDisplay(requiredAccessLevel)} access.");
    }

    private static bool IsBroadcaster(ChannelChatMessage msg) =>
        string.Equals(msg.ChatterUserId, msg.BroadcasterUserId, StringComparison.Ordinal);

    private static string GetAccessDisplay(CommandAccessLevel level) => level switch
    {
        CommandAccessLevel.Subscriber => "subscriber",
        CommandAccessLevel.Vip => "VIP",
        CommandAccessLevel.Moderator => "moderator",
        CommandAccessLevel.Broadcaster => "broadcaster",
        _ => "everyone"
    };
}
