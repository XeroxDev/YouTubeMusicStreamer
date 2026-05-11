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

using Microsoft.AspNetCore.Components;
using TwitchService = YouTubeMusicStreamer.Services.Twitch.TwitchService;
using YouTubeMusicStreamer.Services.App;
using YouTubeMusicStreamer.Services.App.Persistence;
using YouTubeMusicStreamer.Services.Commands;

namespace YouTubeMusicStreamer.Components.Pages.Twitch.Components;

public partial class TwitchCommandSettings(IAppToastService toastService, CommandService commandService, TwitchService twitchService) : ComponentBase
{
    [Parameter] public required CommandDescriptor Command { get; set; }
    [Parameter] public required string CommandPrefix { get; set; }

    private static IReadOnlyList<CommandAccessLevel> AccessLevels { get; } = Enum.GetValues<CommandAccessLevel>();
    private static IReadOnlyList<CommandCooldownScope> CooldownScopes { get; } = Enum.GetValues<CommandCooldownScope>();
    private static IReadOnlyList<ChatTriggerMode> ChatModes { get; } =
        [ChatTriggerMode.Disabled, ChatTriggerMode.Chat, ChatTriggerMode.BitsOnly];
    private static IReadOnlyList<RewardTriggerMode> RewardModes { get; } =
        [RewardTriggerMode.Disabled, RewardTriggerMode.Existing, RewardTriggerMode.AppManaged];

    protected override void OnParametersSet()
    {
        if (Command.CommandConfiguration.RewardTriggerMode == RewardTriggerMode.AppManaged)
            EnsureManagedRewardDraftDefaults();
    }

    private async Task SaveCommandSettingsAsync()
    {
        if (Command.CommandConfiguration.RewardBinding is not null &&
            !string.IsNullOrWhiteSpace(Command.CommandConfiguration.RewardBinding.RewardId))
        {
            Command.CommandConfiguration.RewardBinding.ManagedRewardName = twitchService.Rewards
                .FirstOrDefault(r => string.Equals(r.Id, Command.CommandConfiguration.RewardBinding.RewardId, StringComparison.Ordinal))
                ?.Title;
        }

        await commandService.SaveCommandConfigurationAsync(Command.Name, Command.CommandConfiguration);

        toastService.ShowSuccess("Command settings saved successfully.");
    }

    private ChatTriggerMode ChatMode
    {
        get => TwitchCommandSettingsState.NormalizeChatModeForDisplay(Command.CommandConfiguration.ChatTriggerMode);
        set => TwitchCommandSettingsState.SetChatMode(Command.CommandConfiguration, value);
    }

    private RewardTriggerMode RewardMode
    {
        get => Command.CommandConfiguration.RewardTriggerMode;
        set => TwitchCommandSettingsState.SetRewardMode(
            Command.CommandConfiguration,
            value,
            Command.Name,
            Command.RequiresInput,
            twitchService.BroadcasterAccountId);
    }

    private uint RequiredBits
    {
        get => Command.CommandConfiguration.BitsThreshold;
        set
        {
            Command.CommandConfiguration.BitsThreshold = value;
        }
    }

    private string RewardId
    {
        get => Command.CommandConfiguration.RewardBinding?.RewardId ?? string.Empty;
        set => TwitchCommandSettingsState.SetRewardId(Command.CommandConfiguration, value);
    }

    private CommandAccessLevel RequiredAccessLevel
    {
        get => Command.CommandConfiguration.RequiredAccessLevel;
        set => Command.CommandConfiguration.RequiredAccessLevel = value;
    }

    private CommandCooldownScope CooldownScope
    {
        get => Command.CommandConfiguration.CooldownScope;
        set => Command.CommandConfiguration.CooldownScope = value;
    }

    private static string GetAccessDisplay(CommandAccessLevel level) => level switch
    {
        CommandAccessLevel.Everyone => "Everyone",
        CommandAccessLevel.Subscriber => "Subscriber",
        CommandAccessLevel.Vip => "VIP",
        CommandAccessLevel.Moderator => "Moderator",
        CommandAccessLevel.Broadcaster => "Broadcaster",
        _ => level.ToString()
    };

    private static string GetCooldownScopeDisplay(CommandCooldownScope scope) => scope switch
    {
        CommandCooldownScope.Global => "Global",
        CommandCooldownScope.PerUser => "Per User",
        _ => scope.ToString()
    };

    private static string GetChatModeDisplay(ChatTriggerMode mode) => mode switch
    {
        ChatTriggerMode.Disabled => "Disabled",
        ChatTriggerMode.Chat => "Chat",
        ChatTriggerMode.BitsOnly => "Bits Only",
        _ => mode.ToString()
    };

    private static string GetRewardModeDisplay(RewardTriggerMode mode) => mode switch
    {
        RewardTriggerMode.Disabled => "Disabled",
        RewardTriggerMode.Existing => "Existing Reward",
        RewardTriggerMode.AppManaged => "App-managed Reward",
        _ => mode.ToString()
    };

    private string ManagedRewardTitle
    {
        get => Command.CommandConfiguration.RewardBinding?.ManagedRewardName ?? string.Empty;
        set
        {
            Command.CommandConfiguration.RewardBinding ??= new CommandRewardBindingSnapshot();
            Command.CommandConfiguration.RewardBinding.ManagedRewardName = value;
        }
    }

    private string ManagedRewardPrompt
    {
        get => Command.CommandConfiguration.RewardBinding?.ManagedRewardPrompt ?? string.Empty;
        set
        {
            Command.CommandConfiguration.RewardBinding ??= new CommandRewardBindingSnapshot();
            Command.CommandConfiguration.RewardBinding.ManagedRewardPrompt = value;
        }
    }

    private uint ManagedRewardCost
    {
        get => Command.CommandConfiguration.RewardBinding?.ManagedRewardCost ?? 1000;
        set
        {
            Command.CommandConfiguration.RewardBinding ??= new CommandRewardBindingSnapshot();
            Command.CommandConfiguration.RewardBinding.ManagedRewardCost = value;
        }
    }

    private bool ManagedRewardRequiresUserInput
    {
        get => Command.CommandConfiguration.RewardBinding?.ManagedRewardRequiresUserInput ?? Command.RequiresInput;
        set
        {
            Command.CommandConfiguration.RewardBinding ??= new CommandRewardBindingSnapshot();
            Command.CommandConfiguration.RewardBinding.ManagedRewardRequiresUserInput = value;
        }
    }

    private bool UsesManagedReward => RewardMode == RewardTriggerMode.AppManaged;
    private bool UsesExistingReward => RewardMode == RewardTriggerMode.Existing;
    private bool UsesBitsMode => ChatMode == ChatTriggerMode.BitsOnly;
    private ManagedRewardCompatibilityState ManagedRewardCompatibilityState =>
        Command.CommandConfiguration.RewardBinding?.ManagedRewardCompatibilityState ?? ManagedRewardCompatibilityState.Unknown;
    private string ManagedRewardStatusTagCssClass =>
        $"{TwitchCommandSettingsState.GetManagedRewardCompatibilityTone(ManagedRewardCompatibilityState).ToTagClass()} is-light";
    private bool HasManagedRewardId => !string.IsNullOrWhiteSpace(Command.CommandConfiguration.RewardBinding?.RewardId);
    private bool ShowsCreateAction => TwitchCommandSettingsState.ShowsCreateAction(Command.CommandConfiguration);
    private bool ShowsUpdateAction => TwitchCommandSettingsState.ShowsUpdateAction(Command.CommandConfiguration);
    private string PrimaryManagedRewardActionLabel => TwitchCommandSettingsState.GetPrimaryManagedRewardAction(Command.CommandConfiguration) switch
    {
        ManagedRewardActionKind.Recreate => "Recreate",
        ManagedRewardActionKind.FixOnTwitch => "Fix on Twitch",
        ManagedRewardActionKind.Create => "Create",
        _ => "Update"
    };
    private string ManagedRewardActionHelpText => ManagedRewardCompatibilityState switch
    {
        ManagedRewardCompatibilityState.Missing => "Recreate restores the missing Twitch reward. Refresh syncs Twitch-side changes back into the app.",
        ManagedRewardCompatibilityState.Incompatible => "Fix on Twitch updates the linked reward to match the command requirements. Refresh syncs Twitch-side changes back into the app.",
        _ when !HasManagedRewardId => "Create creates the managed reward on Twitch. Refresh syncs Twitch-side changes back into the app.",
        _ => "Update pushes your current managed reward draft to Twitch. Refresh syncs Twitch-side changes back into the app."
    };

    private async Task CreateManagedRewardAsync()
    {
        EnsureManagedRewardDraftDefaults();
        var result = await twitchService.CreateManagedRewardAsync(Command.CommandConfiguration.RewardBinding!, Command.RequiresInput);
        Command.CommandConfiguration.RewardBinding = result.Binding;
        await SaveCommandSettingsAsync();
        toastService.ShowSuccess("Managed Twitch reward created successfully.");
    }

    private async Task UpdateManagedRewardAsync()
    {
        EnsureManagedRewardDraftDefaults();
        var result = await twitchService.UpdateManagedRewardAsync(Command.CommandConfiguration.RewardBinding!, Command.RequiresInput);
        Command.CommandConfiguration.RewardBinding = result.Binding;
        await SaveCommandSettingsAsync();
        toastService.ShowSuccess("Managed Twitch reward updated successfully.");
    }

    private async Task RefreshManagedRewardAsync()
    {
        EnsureManagedRewardDraftDefaults();
        var result = await twitchService.RefreshManagedRewardBindingAsync(Command.CommandConfiguration.RewardBinding!, Command.RequiresInput);
        Command.CommandConfiguration.RewardBinding = result.Binding;
        await SaveCommandSettingsAsync();
        toastService.ShowSuccess(result.ExistsOnTwitch ? "Managed Twitch reward refreshed successfully." : "Managed Twitch reward is missing on Twitch.");
    }

    private void EnsureManagedRewardDraftDefaults()
    {
        TwitchCommandSettingsState.EnsureManagedRewardDraftDefaults(
            Command.CommandConfiguration,
            Command.Name,
            Command.RequiresInput,
            twitchService.BroadcasterAccountId);
    }
}
