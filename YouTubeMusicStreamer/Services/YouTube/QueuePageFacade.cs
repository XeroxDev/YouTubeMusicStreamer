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

using YouTubeMusicStreamer.Models;
using YouTubeMusicStreamer.Services.App;
using YouTubeMusicStreamer.Services.App.Persistence;

namespace YouTubeMusicStreamer.Services.YouTube;

public sealed class QueuePageFacade(SettingsService settingsService) : IDisposable
{
    private bool _initialized;

    public event EventHandler? StateChanged;

    public IReadOnlyList<QueueItem> QueueItems { get; private set; } = [];
    public QueueSettingsSnapshot Settings { get; private set; } = new(false);

    public void Initialize()
    {
        if (_initialized)
            return;

        _initialized = true;
        Settings = settingsService.GetQueueSettings();
        QueueItems = settingsService.GetQueueItems();
        settingsService.QueueSettingsChanged += HandleQueueSettingsChanged;
        settingsService.QueueItemsChanged += HandleQueueItemsChanged;
    }

    public async Task SaveSettingsAsync(QueueSettingsSnapshot snapshot)
    {
        await settingsService.SaveQueueSettingsAsync(snapshot);
    }

    public async Task RemoveItemAsync(QueueItem item)
    {
        await settingsService.UpdateQueueItemsAsync(items => items.RemoveAll(x => AreSameQueueItem(x, item)));
    }

    public async Task ClearQueueAsync()
    {
        await settingsService.ReplaceQueueItemsAsync([]);
    }

    public async Task MoveItemUpAsync(QueueItem item)
    {
        var index = QueueItems.ToList().FindIndex(x => AreSameQueueItem(x, item));
        if (index <= 0)
            return;

        await settingsService.UpdateQueueItemsAsync(items =>
        {
            var currentIndex = items.FindIndex(x => AreSameQueueItem(x, item));
            if (currentIndex <= 0)
                return;

            items.RemoveAt(currentIndex);
            items.Insert(currentIndex - 1, item);
        });
    }

    public async Task MoveItemDownAsync(QueueItem item)
    {
        var index = QueueItems.ToList().FindIndex(x => AreSameQueueItem(x, item));
        if (index < 0 || index >= QueueItems.Count - 1)
            return;

        await settingsService.UpdateQueueItemsAsync(items =>
        {
            var currentIndex = items.FindIndex(x => AreSameQueueItem(x, item));
            if (currentIndex < 0 || currentIndex >= items.Count - 1)
                return;

            items.RemoveAt(currentIndex);
            items.Insert(currentIndex + 1, item);
        });
    }

    public async Task MoveItemToTopAsync(QueueItem item)
    {
        await settingsService.UpdateQueueItemsAsync(items =>
        {
            var currentIndex = items.FindIndex(x => AreSameQueueItem(x, item));
            if (currentIndex < 0)
                return;

            items.RemoveAt(currentIndex);
            items.Insert(0, item);
        });
    }

    public async Task MoveItemToBottomAsync(QueueItem item)
    {
        await settingsService.UpdateQueueItemsAsync(items =>
        {
            var currentIndex = items.FindIndex(x => AreSameQueueItem(x, item));
            if (currentIndex < 0)
                return;

            items.RemoveAt(currentIndex);
            items.Add(item);
        });
    }

    public void Dispose()
    {
        if (!_initialized)
            return;

        settingsService.QueueSettingsChanged -= HandleQueueSettingsChanged;
        settingsService.QueueItemsChanged -= HandleQueueItemsChanged;
        _initialized = false;
        GC.SuppressFinalize(this);
    }

    private void HandleQueueSettingsChanged(QueueSettingsSnapshot snapshot)
    {
        Settings = snapshot;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void HandleQueueItemsChanged(IReadOnlyList<QueueItem> queueItems)
    {
        QueueItems = queueItems;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private static bool AreSameQueueItem(QueueItem left, QueueItem right) =>
        left.Id == right.Id &&
        left.Requester == right.Requester &&
        left.Message == right.Message &&
        left.RequestedAt == right.RequestedAt;
}
