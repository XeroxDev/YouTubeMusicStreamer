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

namespace YouTubeMusicStreamer.Services.App;

public sealed class AppToastService : IAppToastService, IDisposable
{
    private static readonly TimeSpan NotificationDuration = TimeSpan.FromSeconds(10);
    private readonly Lock _lock = new();
    private readonly Dictionary<Guid, NotificationEntry> _notifications = [];
    private readonly Dictionary<string, Guid> _notificationsByKey = [];
    private readonly List<Guid> _orderedNotifications = [];

    public event EventHandler? NotificationsChanged;

    public IReadOnlyList<AppNotification> Notifications
    {
        get
        {
            lock (_lock)
            {
                return _orderedNotifications
                    .Select(id => _notifications[id].Notification)
                    .ToList();
            }
        }
    }

    public void ShowSuccess(string message) => Show(AppNotificationLevel.Success, message);
    public void ShowInfo(string message) => Show(AppNotificationLevel.Info, message);
    public void ShowWarning(string message) => Show(AppNotificationLevel.Warning, message);
    public void ShowError(string message) => Show(AppNotificationLevel.Error, message);

    public void Dismiss(Guid id)
    {
        if (!TryRemoveNotification(id, null))
            return;

        NotificationsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Pause(Guid id)
    {
        lock (_lock)
        {
            if (!_notifications.TryGetValue(id, out var entry) || entry.IsPaused || !entry.Notification.AutoDismiss)
                return;

            entry.ExpirationCts.Cancel();
            entry.ExpirationCts.Dispose();
            entry.ExpirationCts = new CancellationTokenSource();

            var elapsed = DateTimeOffset.UtcNow - entry.StartedAtUtc;
            var remaining = entry.RemainingDuration - elapsed;
            if (remaining < TimeSpan.Zero)
                remaining = TimeSpan.Zero;

            entry.RemainingDuration = remaining;
            entry.IsPaused = true;
        }
    }

    public void Resume(Guid id)
    {
        NotificationEntry? entry;

        lock (_lock)
        {
            if (!_notifications.TryGetValue(id, out entry) || !entry.IsPaused || !entry.Notification.AutoDismiss)
                return;

            entry.IsPaused = false;
            entry.StartedAtUtc = DateTimeOffset.UtcNow;
            RestartExpiration(entry);
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var entry in _notifications.Values)
            {
                entry.ExpirationCts.Cancel();
                entry.ExpirationCts.Dispose();
            }

            _notifications.Clear();
            _notificationsByKey.Clear();
            _orderedNotifications.Clear();
        }

        GC.SuppressFinalize(this);
    }

    private void Show(AppNotificationLevel level, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        NotificationEntry entry;
        var key = $"{level}:{message}";

        lock (_lock)
        {
            if (_notificationsByKey.TryGetValue(key, out var existingId))
            {
                entry = _notifications[existingId];
                entry.Notification = entry.Notification with
                {
                    RepeatCount = entry.Notification.RepeatCount + 1,
                    Duration = NotificationDuration,
                    Revision = entry.Notification.Revision + 1
                };
                MoveToFront(existingId);
            }
            else
            {
                entry = new NotificationEntry(
                    new AppNotification(Guid.NewGuid(), level, message, 1, true, NotificationDuration, 0),
                    key,
                    new CancellationTokenSource());
                _notifications[entry.Notification.Id] = entry;
                _notificationsByKey[key] = entry.Notification.Id;
                _orderedNotifications.Insert(0, entry.Notification.Id);
            }

            entry.RemainingDuration = NotificationDuration;
            entry.IsPaused = false;
            entry.StartedAtUtc = DateTimeOffset.UtcNow;
            RestartExpiration(entry);
        }

        NotificationsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RestartExpiration(NotificationEntry entry)
    {
        entry.ExpirationCts.Cancel();
        entry.ExpirationCts.Dispose();
        entry.ExpirationCts = new CancellationTokenSource();

        if (!entry.Notification.AutoDismiss)
            return;

        var expirationCts = entry.ExpirationCts;
        var delay = entry.RemainingDuration;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delay, expirationCts.Token);
                if (!TryRemoveNotification(entry.Notification.Id, expirationCts))
                    return;

                NotificationsChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (OperationCanceledException)
            {
                // ignored
            }
        });
    }

    private bool TryRemoveNotification(Guid id, CancellationTokenSource? expectedCts)
    {
        lock (_lock)
        {
            if (!_notifications.TryGetValue(id, out var entry))
                return false;

            if (expectedCts is not null && !ReferenceEquals(entry.ExpirationCts, expectedCts))
                return false;

            entry.ExpirationCts.Cancel();
            entry.ExpirationCts.Dispose();
            _notifications.Remove(id);
            _notificationsByKey.Remove(entry.Key);
            _orderedNotifications.Remove(id);
            return true;
        }
    }

    private void MoveToFront(Guid id)
    {
        _orderedNotifications.Remove(id);
        _orderedNotifications.Insert(0, id);
    }

    private sealed class NotificationEntry(AppNotification notification, string key, CancellationTokenSource expirationCts)
    {
        public AppNotification Notification { get; set; } = notification;
        public string Key { get; } = key;
        public CancellationTokenSource ExpirationCts { get; set; } = expirationCts;
        public DateTimeOffset StartedAtUtc { get; set; } = DateTimeOffset.UtcNow;
        public TimeSpan RemainingDuration { get; set; } = notification.Duration;
        public bool IsPaused { get; set; }
    }
}
