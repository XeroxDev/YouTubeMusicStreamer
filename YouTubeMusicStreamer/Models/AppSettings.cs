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

using Microsoft.Extensions.Logging;
using YouTubeMusicStreamer.Utils;

namespace YouTubeMusicStreamer.Models;

public class AppSettings
{
    #region Application Settings

    public LogLevel LogLevel { get; set; }
    public bool EnablePreRelease { get; set; }

    #endregion

    #region YTMDesktop Settings

    public string? YouTubeHost { get; set; }
    public int? YouTubePort { get; set; }

    public List<BlacklistEntry> Blacklist { get; init; } = [];

    public bool AutoStartServer { get; set; }

    public int PublicPort { get; set; } = 9876;

    public bool AllowAudioCapture { get; set; }

    public string AudioCaptureDevice { get; set; } = string.Empty;

    #endregion

    #region Twitch Settings

    public string TwitchChatChannel { get; set; } = string.Empty;

    public bool TwitchSendMessageOnConnect { get; set; } = true;
    public string TwitchConnectMessage { get; set; } = $"{AppUtils.AppName} connected!";

    public string TwitchCommandPrefix { get; set; } = "!";
    public Dictionary<string, CommandSettings> Commands { get; set; } = new();

    #endregion

    #region Queue Settings

    public bool QueueActive { get; set; }
    public bool QueueItemAsIFrame { get; set; } = false;
    public List<QueueItem> Queue { get; init; } = [];

    #endregion
}