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

using Microsoft.Extensions.Logging;

namespace YouTubeMusicStreamer.Services.App.Persistence;

public enum ChatTriggerMode
{
    Disabled,
    Chat,
    BitsOnly,
    ChatAndBits
}

public enum RewardTriggerMode
{
    Disabled,
    Existing,
    AppManaged
}

public enum ManagedRewardCompatibilityState
{
    Unknown,
    Compatible,
    Missing,
    Incompatible
}

public enum CommandAccessLevel
{
    Everyone,
    Subscriber,
    Vip,
    Moderator,
    Broadcaster
}

public enum CommandCooldownScope
{
    Global,
    PerUser
}

public sealed record AppConfigurationSnapshot(
    LogLevel LogLevel);

public sealed record YouTubeSettingsSnapshot(
    string? Host,
    int? Port,
    bool AutoStartServer,
    int PublicPort,
    bool AllowAudioCapture,
    string AudioCaptureDevice);

public sealed record TwitchAccountMetadataSnapshot(
    string? AccountId,
    string? Login,
    string? DisplayName,
    string? ProfileImageUrl);

public sealed record TwitchSettingsSnapshot(
    bool SendMessageOnConnect,
    string ConnectMessage,
    string CommandPrefix,
    TwitchAccountMetadataSnapshot? BroadcasterAccount,
    TwitchAccountMetadataSnapshot? BotAccount);

public sealed record QueueSettingsSnapshot(
    bool QueueActive);

public sealed class CommandRewardBindingSnapshot
{
    public string? RewardId { get; set; }
    public string? BroadcasterAccountId { get; set; }
    public string? ManagedRewardName { get; set; }
    public string? ManagedRewardPrompt { get; set; }
    public uint ManagedRewardCost { get; set; }
    public bool ManagedRewardRequiresUserInput { get; set; }
    public ManagedRewardCompatibilityState ManagedRewardCompatibilityState { get; set; }
    public string? ManagedRewardCompatibilityMessage { get; set; }

    public CommandRewardBindingSnapshot Clone() => new()
    {
        RewardId = RewardId,
        BroadcasterAccountId = BroadcasterAccountId,
        ManagedRewardName = ManagedRewardName,
        ManagedRewardPrompt = ManagedRewardPrompt,
        ManagedRewardCost = ManagedRewardCost,
        ManagedRewardRequiresUserInput = ManagedRewardRequiresUserInput,
        ManagedRewardCompatibilityState = ManagedRewardCompatibilityState,
        ManagedRewardCompatibilityMessage = ManagedRewardCompatibilityMessage
    };
}

public sealed class CommandConfigurationSnapshot
{
    public required string Trigger { get; set; }
    public ChatTriggerMode ChatTriggerMode { get; set; }
    public RewardTriggerMode RewardTriggerMode { get; set; }
    public CommandAccessLevel RequiredAccessLevel { get; set; }
    public CommandCooldownScope CooldownScope { get; set; }
    public uint BitsThreshold { get; set; }
    public CommandRewardBindingSnapshot? RewardBinding { get; set; }
    public uint Cooldown { get; set; }
    public string? Response { get; set; }
    public string? AccessDeniedResponse { get; set; }

    public bool IsEnabled => ChatTriggerMode != ChatTriggerMode.Disabled || RewardTriggerMode != RewardTriggerMode.Disabled;
    public uint RequiredBits => ChatTriggerMode == ChatTriggerMode.BitsOnly ? BitsThreshold : 0;
    public string? RewardId => RewardBinding?.RewardId;

    public CommandConfigurationSnapshot Clone() => new()
    {
        Trigger = Trigger,
        ChatTriggerMode = ChatTriggerMode,
        RewardTriggerMode = RewardTriggerMode,
        RequiredAccessLevel = RequiredAccessLevel,
        CooldownScope = CooldownScope,
        BitsThreshold = BitsThreshold,
        RewardBinding = RewardBinding?.Clone(),
        Cooldown = Cooldown,
        Response = Response,
        AccessDeniedResponse = AccessDeniedResponse
    };
}
