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

using System.Diagnostics;

namespace YouTubeMusicStreamer.Services.App;

public sealed class LogViewerEnvironment(IAppPathProvider appPathProvider) : ILogViewerEnvironment
{
    public string LogFolderPath => appPathProvider.LogDirectoryPath;
    public string CurrentLogFileName => appPathProvider.CurrentLogFileName;

    public IDisposable WatchLogFile(string directoryPath, string fileName, Action<string> onChanged)
    {
        var fileSystemWatcher = new FileSystemWatcher
        {
            Path = directoryPath,
            Filter = fileName,
            NotifyFilter = NotifyFilters.LastWrite,
            EnableRaisingEvents = true
        };

        FileSystemEventHandler handler = (_, args) => onChanged(args.FullPath);
        fileSystemWatcher.Changed += handler;
        return new FileWatcherSubscription(fileSystemWatcher, handler);
    }

    public IReadOnlyList<string> ReadLogLines(string fullPath)
    {
        if (!File.Exists(fullPath))
            return [];

        var lines = new List<string>();
        using var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var streamReader = new StreamReader(fileStream);

        while (streamReader.ReadLine() is { } line)
        {
            lines.Add(line);
        }

        return lines;
    }

    public void OpenFolder(string folderPath)
    {
        Process.Start(new ProcessStartInfo("explorer.exe", folderPath));
    }

    public Task SetClipboardTextAsync(string text) => Clipboard.Default.SetTextAsync(text);

    private sealed class FileWatcherSubscription(
        FileSystemWatcher fileSystemWatcher,
        FileSystemEventHandler changedHandler) : IDisposable
    {
        public void Dispose()
        {
            fileSystemWatcher.Changed -= changedHandler;
            fileSystemWatcher.Dispose();
        }
    }
}
