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

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace YouTubeMusicStreamer.Services.App.Persistence;

[Table("CommandConfigurations")]
public sealed class CommandConfigurationEntity
{
    [Key]
    [Required]
    public string CommandKey { get; set; } = string.Empty;

    [Required]
    public string Trigger { get; set; } = string.Empty;

    [Required]
    public ChatTriggerMode ChatTriggerMode { get; set; }

    [Required]
    public RewardTriggerMode RewardTriggerMode { get; set; }

    [Required]
    public CommandAccessLevel RequiredAccessLevel { get; set; }

    [Required]
    public CommandCooldownScope CooldownScope { get; set; }

    public uint BitsThreshold { get; set; }
    public string? RewardId { get; set; }
    public string? RewardBroadcasterAccountId { get; set; }
    public string? ManagedRewardName { get; set; }
    public string? ManagedRewardPrompt { get; set; }
    public uint ManagedRewardCost { get; set; }
    public bool ManagedRewardRequiresUserInput { get; set; }
    public ManagedRewardCompatibilityState ManagedRewardCompatibilityState { get; set; }
    public string? ManagedRewardCompatibilityMessage { get; set; }
    public uint Cooldown { get; set; }
    public string? Response { get; set; }
    public string? AccessDeniedResponse { get; set; }
}
