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

using System.ComponentModel;
using YouTubeMusicStreamer.Services.Twitch;
using YouTubeMusicStreamer.Services.WebSocket;
using YouTubeMusicStreamer.Services.YouTube;

namespace YouTubeMusicStreamer.Services.App;

public interface ITwitchStatusSource : INotifyPropertyChanged
{
    TwitchSessionStatus SessionStatus { get; }
    string? Username { get; }
    string? BroadcasterAccountId { get; }
    string? BotUsername { get; }
}

public interface IYouTubeStatusSource : INotifyPropertyChanged
{
    YtmCompanionSessionState SessionState { get; }
    string Error { get; }
}

public interface IWidgetServerStatusSource
{
    WidgetServerState State { get; }
    event EventHandler<WidgetServerState> StateChanged;
}

public interface IVersionStatusSource
{
    VersionService.UpdateStatus Status { get; }
    string StatusText { get; }
    event Action? OnChange;
}

public interface ILogViewerEnvironment
{
    string LogFolderPath { get; }
    string CurrentLogFileName { get; }
    IDisposable WatchLogFile(string directoryPath, string fileName, Action<string> onChanged);
    IReadOnlyList<string> ReadLogLines(string fullPath);
    void OpenFolder(string folderPath);
    Task SetClipboardTextAsync(string text);
}

