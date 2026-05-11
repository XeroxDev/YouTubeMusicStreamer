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

[Table("TwitchSettings")]
public sealed class TwitchSettingsEntity
{
    [Key]
    public int Id { get; set; }

    public bool SendMessageOnConnect { get; set; } = true;

    [Required]
    public string ConnectMessage { get; set; } = string.Empty;

    [Required]
    public string CommandPrefix { get; set; } = "!";

    public string? BroadcasterAccountId { get; set; }
    public string? BroadcasterLogin { get; set; }
    public string? BroadcasterDisplayName { get; set; }
    public string? BroadcasterProfileImageUrl { get; set; }
    public string? BotAccountId { get; set; }
    public string? BotLogin { get; set; }
    public string? BotDisplayName { get; set; }
    public string? BotProfileImageUrl { get; set; }
}
