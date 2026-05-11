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

using YouTubeMusicStreamer.Components;
using YouTubeMusicStreamer.Services.App.Persistence;

namespace YouTubeMusicStreamer.Components.Pages.Twitch.Components;

internal enum ManagedRewardActionKind
{
    Create,
    Update,
    Recreate,
    FixOnTwitch
}

internal static class TwitchCommandSettingsState
{
    public static ChatTriggerMode NormalizeChatModeForDisplay(ChatTriggerMode mode) =>
        mode == ChatTriggerMode.ChatAndBits ? ChatTriggerMode.BitsOnly : mode;

    public static void SetChatMode(CommandConfigurationSnapshot configuration, ChatTriggerMode mode)
    {
        configuration.ChatTriggerMode = mode;

        if (mode == ChatTriggerMode.BitsOnly && configuration.BitsThreshold == 0)
            configuration.BitsThreshold = 100;
    }

    public static void SetRewardMode(
        CommandConfigurationSnapshot configuration,
        RewardTriggerMode mode,
        string commandName,
        bool requiresInput,
        string? broadcasterAccountId)
    {
        configuration.RewardTriggerMode = mode;

        if (mode == RewardTriggerMode.Disabled)
        {
            configuration.RewardBinding = null;
            return;
        }

        configuration.RewardBinding ??= new CommandRewardBindingSnapshot();
        if (mode == RewardTriggerMode.Existing)
        {
            configuration.RewardBinding.ManagedRewardCompatibilityState = ManagedRewardCompatibilityState.Unknown;
            configuration.RewardBinding.ManagedRewardCompatibilityMessage = null;
            return;
        }

        EnsureManagedRewardDraftDefaults(configuration, commandName, requiresInput, broadcasterAccountId);
    }

    public static void SetRewardId(CommandConfigurationSnapshot configuration, string? rewardId)
    {
        if (string.IsNullOrWhiteSpace(rewardId))
        {
            configuration.RewardBinding = null;
            configuration.RewardTriggerMode = RewardTriggerMode.Disabled;
            return;
        }

        configuration.RewardBinding ??= new CommandRewardBindingSnapshot();
        configuration.RewardBinding.RewardId = rewardId;
        configuration.RewardBinding.ManagedRewardName = null;

        if (configuration.RewardTriggerMode == RewardTriggerMode.Disabled)
            configuration.RewardTriggerMode = RewardTriggerMode.Existing;
    }

    public static void EnsureManagedRewardDraftDefaults(
        CommandConfigurationSnapshot configuration,
        string commandName,
        bool requiresInput,
        string? broadcasterAccountId)
    {
        configuration.RewardBinding ??= new CommandRewardBindingSnapshot();
        var binding = configuration.RewardBinding;
        binding.BroadcasterAccountId ??= broadcasterAccountId;
        if (string.IsNullOrWhiteSpace(binding.ManagedRewardName))
            binding.ManagedRewardName = BuildDefaultManagedRewardTitle(commandName);
        binding.ManagedRewardCost = binding.ManagedRewardCost == 0 ? 1000u : binding.ManagedRewardCost;
        binding.ManagedRewardRequiresUserInput = requiresInput || binding.ManagedRewardRequiresUserInput;

        if (binding.ManagedRewardCompatibilityState == ManagedRewardCompatibilityState.Unknown &&
            string.IsNullOrWhiteSpace(binding.ManagedRewardCompatibilityMessage))
        {
            binding.ManagedRewardCompatibilityMessage = string.IsNullOrWhiteSpace(binding.RewardId)
                ? "Managed reward has not been created on Twitch yet."
                : null;
        }
    }

    public static bool ShowsCreateAction(CommandConfigurationSnapshot configuration) =>
        !HasManagedRewardId(configuration) ||
        GetManagedRewardCompatibilityState(configuration) == ManagedRewardCompatibilityState.Missing;

    public static bool ShowsUpdateAction(CommandConfigurationSnapshot configuration) =>
        HasManagedRewardId(configuration) &&
        GetManagedRewardCompatibilityState(configuration) != ManagedRewardCompatibilityState.Missing;

    public static ManagedRewardActionKind GetPrimaryManagedRewardAction(CommandConfigurationSnapshot configuration)
    {
        var state = GetManagedRewardCompatibilityState(configuration);
        if (state == ManagedRewardCompatibilityState.Missing)
            return ManagedRewardActionKind.Recreate;

        if (state == ManagedRewardCompatibilityState.Incompatible)
            return ManagedRewardActionKind.FixOnTwitch;

        return HasManagedRewardId(configuration) ? ManagedRewardActionKind.Update : ManagedRewardActionKind.Create;
    }

    public static UiStatusTone GetManagedRewardCompatibilityTone(ManagedRewardCompatibilityState state) => state switch
    {
        ManagedRewardCompatibilityState.Compatible => UiStatusTone.Success,
        ManagedRewardCompatibilityState.Missing => UiStatusTone.Warning,
        ManagedRewardCompatibilityState.Incompatible => UiStatusTone.Danger,
        _ => UiStatusTone.Info
    };

    private static ManagedRewardCompatibilityState GetManagedRewardCompatibilityState(CommandConfigurationSnapshot configuration) =>
        configuration.RewardBinding?.ManagedRewardCompatibilityState ?? ManagedRewardCompatibilityState.Unknown;

    private static bool HasManagedRewardId(CommandConfigurationSnapshot configuration) =>
        !string.IsNullOrWhiteSpace(configuration.RewardBinding?.RewardId);

    private static string BuildDefaultManagedRewardTitle(string commandName)
    {
        var title = commandName.EndsWith("Command", StringComparison.Ordinal)
            ? commandName[..^"Command".Length]
            : commandName;

        return string.IsNullOrWhiteSpace(title)
            ? "Command Reward"
            : title.Trim();
    }
}
