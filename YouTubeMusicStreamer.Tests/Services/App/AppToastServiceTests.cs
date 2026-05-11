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

using System.Reflection;
using YouTubeMusicStreamer.Services.App;

namespace YouTubeMusicStreamer.Tests.Services.App;

public sealed class AppToastServiceTests
{
    [Fact]
    public void ShowError_IgnoresWhitespaceMessage()
    {
        using var service = new AppToastService();
        var changedCount = 0;
        service.NotificationsChanged += (_, _) => changedCount++;

        service.ShowError("   ");

        Assert.Empty(service.Notifications);
        Assert.Equal(0, changedCount);
    }

    [Fact]
    public void ShowInfo_IncrementsRepeatCountAndMovesDuplicateToFront()
    {
        using var service = new AppToastService();

        service.ShowWarning("Other");
        service.ShowInfo("Duplicate");
        var original = service.Notifications.Single(x => x.Message == "Duplicate");

        service.ShowInfo("Duplicate");

        Assert.Equal(2, service.Notifications.Count);
        var updated = Assert.Single(service.Notifications, x => x.Message == "Duplicate");
        Assert.Equal("Duplicate", service.Notifications[0].Message);
        Assert.Equal(2, updated.RepeatCount);
        Assert.Equal(original.Revision + 1, updated.Revision);
        Assert.Equal(original.Id, updated.Id);
    }

    [Fact]
    public async Task Pause_PreventsAutoDismissUntilResume()
    {
        using var service = new AppToastService();
        service.ShowError("Pause me");
        var notification = Assert.Single(service.Notifications);

        SetRemainingDuration(service, notification.Id, TimeSpan.FromMilliseconds(50));
        service.Pause(notification.Id);

        await Task.Delay(120);

        Assert.Single(service.Notifications);

        service.Resume(notification.Id);

        await WaitUntilAsync(() => service.Notifications.Count == 0, TimeSpan.FromSeconds(1));

        Assert.Empty(service.Notifications);
    }

    [Fact]
    public async Task ShowInfo_CancelsStaleExpiration_WhenDuplicateNotificationIsRefreshed()
    {
        using var service = new AppToastService();
        service.ShowInfo("Refresh me");
        var notification = Assert.Single(service.Notifications);

        SetRemainingDuration(service, notification.Id, TimeSpan.FromMilliseconds(50));
        RestartExpiration(service, notification.Id);

        service.ShowInfo("Refresh me");

        await Task.Delay(120);

        var refreshed = Assert.Single(service.Notifications, x => x.Message == "Refresh me");
        Assert.Equal(2, refreshed.RepeatCount);
    }

    [Fact]
    public void Dismiss_RemovesNotification_AndRaisesChangedEventOnce()
    {
        using var service = new AppToastService();
        service.ShowSuccess("Dismiss me");
        var notification = Assert.Single(service.Notifications);
        var changedCount = 0;
        service.NotificationsChanged += (_, _) => changedCount++;

        service.Dismiss(notification.Id);

        Assert.Empty(service.Notifications);
        Assert.Equal(1, changedCount);
    }

    [Fact]
    public async Task Dispose_CancelsPendingExpirationWithoutLateNotificationsChanged()
    {
        var service = new AppToastService();
        service.ShowWarning("Dispose me");
        var notification = Assert.Single(service.Notifications);
        var changedCount = 0;
        service.NotificationsChanged += (_, _) => changedCount++;

        SetRemainingDuration(service, notification.Id, TimeSpan.FromMilliseconds(50));
        RestartExpiration(service, notification.Id);

        service.Dispose();

        await Task.Delay(120);

        Assert.Equal(0, changedCount);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var start = DateTime.UtcNow;
        while (!condition())
        {
            if (DateTime.UtcNow - start > timeout)
                throw new TimeoutException("Condition was not met in time.");

            await Task.Delay(10);
        }
    }

    private static void RestartExpiration(AppToastService service, Guid id)
    {
        var entry = GetEntry(service, id);
        var restartMethod = typeof(AppToastService).GetMethod("RestartExpiration", BindingFlags.Instance | BindingFlags.NonPublic)
                           ?? throw new InvalidOperationException("RestartExpiration was not found.");
        restartMethod.Invoke(service, [entry]);
    }

    private static void SetRemainingDuration(AppToastService service, Guid id, TimeSpan duration)
    {
        var entry = GetEntry(service, id);
        var entryType = entry.GetType();
        var remainingDurationProperty = entryType.GetProperty("RemainingDuration", BindingFlags.Instance | BindingFlags.Public)
                                        ?? throw new InvalidOperationException("RemainingDuration property was not found.");
        var startedAtProperty = entryType.GetProperty("StartedAtUtc", BindingFlags.Instance | BindingFlags.Public)
                                ?? throw new InvalidOperationException("StartedAtUtc property was not found.");
        remainingDurationProperty.SetValue(entry, duration);
        startedAtProperty.SetValue(entry, DateTimeOffset.UtcNow);
    }

    private static object GetEntry(AppToastService service, Guid id)
    {
        var notificationsField = typeof(AppToastService).GetField("_notifications", BindingFlags.Instance | BindingFlags.NonPublic)
                                 ?? throw new InvalidOperationException("_notifications field was not found.");
        var dictionary = notificationsField.GetValue(service) as System.Collections.IDictionary
                         ?? throw new InvalidOperationException("_notifications is not a dictionary.");

        return dictionary[id] ?? throw new InvalidOperationException($"Notification '{id}' was not found.");
    }
}
