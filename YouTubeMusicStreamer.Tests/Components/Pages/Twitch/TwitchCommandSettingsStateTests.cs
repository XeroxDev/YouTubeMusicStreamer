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
using YouTubeMusicStreamer.Components.Pages.Twitch.Components;
using YouTubeMusicStreamer.Services.App.Persistence;

namespace YouTubeMusicStreamer.Tests.Components.Pages.Twitch;

public sealed class TwitchCommandSettingsStateTests
{
    [Fact]
    public void NormalizeChatModeForDisplay_HidesLegacyChatAndBitsMode()
    {
        var mode = TwitchCommandSettingsState.NormalizeChatModeForDisplay(ChatTriggerMode.ChatAndBits);

        Assert.Equal(ChatTriggerMode.BitsOnly, mode);
    }

    [Fact]
    public void SetChatMode_SeedsBitsThresholdOnlyWhenBitsModeWouldOtherwiseBeUnusable()
    {
        var configuration = CreateConfiguration(bitsThreshold: 0);

        TwitchCommandSettingsState.SetChatMode(configuration, ChatTriggerMode.BitsOnly);

        Assert.Equal(ChatTriggerMode.BitsOnly, configuration.ChatTriggerMode);
        Assert.Equal(100u, configuration.BitsThreshold);

        TwitchCommandSettingsState.SetChatMode(configuration, ChatTriggerMode.Chat);
        TwitchCommandSettingsState.SetChatMode(configuration, ChatTriggerMode.BitsOnly);

        Assert.Equal(100u, configuration.BitsThreshold);
    }

    [Fact]
    public void SetRewardMode_DisabledClearsExistingBinding()
    {
        var configuration = CreateConfiguration(
            rewardTriggerMode: RewardTriggerMode.AppManaged,
            rewardBinding: new CommandRewardBindingSnapshot { RewardId = "reward-1" });

        TwitchCommandSettingsState.SetRewardMode(configuration, RewardTriggerMode.Disabled, "RequestCommand", false, "broadcaster-1");

        Assert.Equal(RewardTriggerMode.Disabled, configuration.RewardTriggerMode);
        Assert.Null(configuration.RewardBinding);
    }

    [Fact]
    public void SetRewardMode_ExistingClearsStaleManagedCompatibility()
    {
        var configuration = CreateConfiguration(
            rewardTriggerMode: RewardTriggerMode.AppManaged,
            rewardBinding: new CommandRewardBindingSnapshot
            {
                RewardId = "reward-1",
                ManagedRewardCompatibilityState = ManagedRewardCompatibilityState.Incompatible,
                ManagedRewardCompatibilityMessage = "wrong prompt"
            });

        TwitchCommandSettingsState.SetRewardMode(configuration, RewardTriggerMode.Existing, "RequestCommand", false, "broadcaster-1");

        Assert.Equal(RewardTriggerMode.Existing, configuration.RewardTriggerMode);
        Assert.NotNull(configuration.RewardBinding);
        Assert.Equal(ManagedRewardCompatibilityState.Unknown, configuration.RewardBinding.ManagedRewardCompatibilityState);
        Assert.Null(configuration.RewardBinding.ManagedRewardCompatibilityMessage);
    }

    [Fact]
    public void SetRewardMode_AppManagedCreatesUsableDraftDefaults()
    {
        var configuration = CreateConfiguration();

        TwitchCommandSettingsState.SetRewardMode(configuration, RewardTriggerMode.AppManaged, "RequestCommand", true, "broadcaster-1");

        Assert.Equal(RewardTriggerMode.AppManaged, configuration.RewardTriggerMode);
        Assert.NotNull(configuration.RewardBinding);
        Assert.Equal("broadcaster-1", configuration.RewardBinding.BroadcasterAccountId);
        Assert.Equal("Request", configuration.RewardBinding.ManagedRewardName);
        Assert.Equal(1000u, configuration.RewardBinding.ManagedRewardCost);
        Assert.True(configuration.RewardBinding.ManagedRewardRequiresUserInput);
        Assert.False(string.IsNullOrWhiteSpace(configuration.RewardBinding.ManagedRewardCompatibilityMessage));
    }

    [Fact]
    public void EnsureManagedRewardDraftDefaults_PreservesOperatorChoicesButFillsInvalidTitle()
    {
        var configuration = CreateConfiguration(
            rewardTriggerMode: RewardTriggerMode.AppManaged,
            rewardBinding: new CommandRewardBindingSnapshot
            {
                BroadcasterAccountId = "existing-broadcaster",
                ManagedRewardName = "   ",
                ManagedRewardCost = 42,
                ManagedRewardRequiresUserInput = true
            });

        TwitchCommandSettingsState.EnsureManagedRewardDraftDefaults(configuration, "Command", false, "new-broadcaster");

        Assert.NotNull(configuration.RewardBinding);
        Assert.Equal("existing-broadcaster", configuration.RewardBinding.BroadcasterAccountId);
        Assert.Equal("Command Reward", configuration.RewardBinding.ManagedRewardName);
        Assert.Equal(42u, configuration.RewardBinding.ManagedRewardCost);
        Assert.True(configuration.RewardBinding.ManagedRewardRequiresUserInput);
    }

    [Fact]
    public void SetRewardId_BlankInputDisablesRewardInsteadOfLeavingHalfConfiguredBinding()
    {
        var configuration = CreateConfiguration(
            rewardTriggerMode: RewardTriggerMode.Existing,
            rewardBinding: new CommandRewardBindingSnapshot { RewardId = "reward-1" });

        TwitchCommandSettingsState.SetRewardId(configuration, "   ");

        Assert.Equal(RewardTriggerMode.Disabled, configuration.RewardTriggerMode);
        Assert.Null(configuration.RewardBinding);
    }

    [Fact]
    public void SetRewardId_NonBlankInputCreatesExistingRewardBindingFromDisabledState()
    {
        var configuration = CreateConfiguration();

        TwitchCommandSettingsState.SetRewardId(configuration, "reward-1");

        Assert.Equal(RewardTriggerMode.Existing, configuration.RewardTriggerMode);
        Assert.NotNull(configuration.RewardBinding);
        Assert.Equal("reward-1", configuration.RewardBinding.RewardId);
        Assert.Null(configuration.RewardBinding.ManagedRewardName);
    }

    [Theory]
    [InlineData(null, ManagedRewardCompatibilityState.Unknown, (int)ManagedRewardActionKind.Create, true, false)]
    [InlineData("reward-1", ManagedRewardCompatibilityState.Unknown, (int)ManagedRewardActionKind.Update, false, true)]
    [InlineData("reward-1", ManagedRewardCompatibilityState.Compatible, (int)ManagedRewardActionKind.Update, false, true)]
    [InlineData("reward-1", ManagedRewardCompatibilityState.Missing, (int)ManagedRewardActionKind.Recreate, true, false)]
    [InlineData("reward-1", ManagedRewardCompatibilityState.Incompatible, (int)ManagedRewardActionKind.FixOnTwitch, false, true)]
    public void ManagedRewardActions_FollowRewardExistenceAndCompatibility(
        string? rewardId,
        ManagedRewardCompatibilityState compatibilityState,
        int actionKind,
        bool showsCreate,
        bool showsUpdate)
    {
        var configuration = CreateConfiguration(
            rewardTriggerMode: RewardTriggerMode.AppManaged,
            rewardBinding: new CommandRewardBindingSnapshot
            {
                RewardId = rewardId,
                ManagedRewardCompatibilityState = compatibilityState
            });

        Assert.Equal((ManagedRewardActionKind)actionKind, TwitchCommandSettingsState.GetPrimaryManagedRewardAction(configuration));
        Assert.Equal(showsCreate, TwitchCommandSettingsState.ShowsCreateAction(configuration));
        Assert.Equal(showsUpdate, TwitchCommandSettingsState.ShowsUpdateAction(configuration));
    }

    [Theory]
    [InlineData(ManagedRewardCompatibilityState.Unknown, (int)UiStatusTone.Info)]
    [InlineData(ManagedRewardCompatibilityState.Compatible, (int)UiStatusTone.Success)]
    [InlineData(ManagedRewardCompatibilityState.Missing, (int)UiStatusTone.Warning)]
    [InlineData(ManagedRewardCompatibilityState.Incompatible, (int)UiStatusTone.Danger)]
    public void CompatibilityTone_ReflectsOperationalRisk(ManagedRewardCompatibilityState state, int expectedTone)
    {
        Assert.Equal((UiStatusTone)expectedTone, TwitchCommandSettingsState.GetManagedRewardCompatibilityTone(state));
    }

    private static CommandConfigurationSnapshot CreateConfiguration(
        uint bitsThreshold = 0,
        RewardTriggerMode rewardTriggerMode = RewardTriggerMode.Disabled,
        CommandRewardBindingSnapshot? rewardBinding = null) =>
        new()
        {
            Trigger = "request",
            ChatTriggerMode = ChatTriggerMode.Chat,
            RewardTriggerMode = rewardTriggerMode,
            RequiredAccessLevel = CommandAccessLevel.Everyone,
            CooldownScope = CommandCooldownScope.Global,
            BitsThreshold = bitsThreshold,
            RewardBinding = rewardBinding
        };
}
