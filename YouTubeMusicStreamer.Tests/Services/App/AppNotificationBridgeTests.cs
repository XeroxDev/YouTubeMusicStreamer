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

using YouTubeMusicStreamer.Services.App;
using YouTubeMusicStreamer.Services.App.Diagnostics;

namespace YouTubeMusicStreamer.Tests.Services.App;

public sealed class AppNotificationBridgeTests
{
    [Fact]
    public void Initialize_IsIdempotent_AndForwardsToastDiagnosticsOnce()
    {
        var toast = new FakeToastService();
        var diagnostics = new FakeDiagnosticsService();
        using var bridge = new AppNotificationBridge(toast, diagnostics);

        bridge.Initialize();
        bridge.Initialize();

        diagnostics.Raise(
            AppDiagnosticSubsystem.Commands,
            AppDiagnosticSeverity.Info,
            AppDiagnosticCategory.Configuration,
            "Toast me",
            visibility: AppDiagnosticVisibility.Toast);

        Assert.Single(toast.NotificationsLog);
        var notification = Assert.Single(toast.InfoMessages);
        Assert.Equal("Toast me", notification);
        Assert.Equal((AppNotificationLevel.Info, "Toast me"), toast.NotificationsLog[0]);
    }

    [Fact]
    public void HandleDiagnosticRecorded_MapsSeverities_AndIgnoresNonToastDiagnostics()
    {
        var toast = new FakeToastService();
        var diagnostics = new FakeDiagnosticsService();
        using var bridge = new AppNotificationBridge(toast, diagnostics);

        bridge.Initialize();

        diagnostics.Raise(
            AppDiagnosticSubsystem.Commands,
            AppDiagnosticSeverity.Info,
            AppDiagnosticCategory.Configuration,
            "Info toast",
            visibility: AppDiagnosticVisibility.Toast);
        diagnostics.Raise(
            AppDiagnosticSubsystem.Commands,
            AppDiagnosticSeverity.Warning,
            AppDiagnosticCategory.Configuration,
            "Warning toast",
            visibility: AppDiagnosticVisibility.Toast);
        diagnostics.Raise(
            AppDiagnosticSubsystem.Commands,
            AppDiagnosticSeverity.Error,
            AppDiagnosticCategory.Configuration,
            "Error toast",
            visibility: AppDiagnosticVisibility.Toast);
        diagnostics.Raise(
            AppDiagnosticSubsystem.Commands,
            AppDiagnosticSeverity.Error,
            AppDiagnosticCategory.Configuration,
            "Hidden diagnostic",
            visibility: AppDiagnosticVisibility.DiagnosticsOnly);

        Assert.Equal(
            [
                (AppNotificationLevel.Info, "Info toast"),
                (AppNotificationLevel.Warning, "Warning toast"),
                (AppNotificationLevel.Error, "Error toast")
            ],
            toast.NotificationsLog);
    }

    [Fact]
    public void Dispose_UnsubscribesFromDiagnostics()
    {
        var toast = new FakeToastService();
        var diagnostics = new FakeDiagnosticsService();
        var bridge = new AppNotificationBridge(toast, diagnostics);

        bridge.Initialize();
        bridge.Dispose();

        diagnostics.Raise(
            AppDiagnosticSubsystem.Commands,
            AppDiagnosticSeverity.Info,
            AppDiagnosticCategory.Configuration,
            "Toast me",
            visibility: AppDiagnosticVisibility.Toast);

        Assert.Empty(toast.NotificationsLog);
    }

    private sealed class FakeToastService : IAppToastService
    {
        public event EventHandler? NotificationsChanged
        {
            add { }
            remove { }
        }

        public IReadOnlyList<AppNotification> Notifications { get; } = [];

        public List<(AppNotificationLevel Level, string Message)> NotificationsLog { get; } = [];

        public List<string> InfoMessages { get; } = [];

        public void ShowSuccess(string message) => NotificationsLog.Add((AppNotificationLevel.Success, message));

        public void ShowInfo(string message)
        {
            NotificationsLog.Add((AppNotificationLevel.Info, message));
            InfoMessages.Add(message);
        }

        public void ShowWarning(string message) => NotificationsLog.Add((AppNotificationLevel.Warning, message));

        public void ShowError(string message) => NotificationsLog.Add((AppNotificationLevel.Error, message));

        public void Dismiss(Guid id) { }

        public void Pause(Guid id) { }

        public void Resume(Guid id) { }
    }

    private sealed class FakeDiagnosticsService : IAppDiagnosticsService
    {
        public event EventHandler<AppDiagnostic>? DiagnosticRecorded;

        public IReadOnlyList<AppDiagnostic> RecentDiagnostics { get; } = [];

        public Task ClearAsync() => Task.CompletedTask;

        public AppDiagnostic Record(
            AppDiagnosticSubsystem subsystem,
            AppDiagnosticSeverity severity,
            AppDiagnosticCategory category,
            string summary,
            string? detail = null,
            Exception? exception = null,
            AppDiagnosticVisibility visibility = AppDiagnosticVisibility.DiagnosticsOnly)
        {
            var diagnostic = new AppDiagnostic(
                Guid.NewGuid(),
                subsystem,
                severity,
                category,
                summary,
                detail,
                exception,
                exception?.ToString(),
                DateTimeOffset.UtcNow,
                visibility);
            DiagnosticRecorded?.Invoke(this, diagnostic);
            return diagnostic;
        }

        public void Raise(
            AppDiagnosticSubsystem subsystem,
            AppDiagnosticSeverity severity,
            AppDiagnosticCategory category,
            string summary,
            string? detail = null,
            Exception? exception = null,
            AppDiagnosticVisibility visibility = AppDiagnosticVisibility.DiagnosticsOnly)
        {
            var diagnostic = new AppDiagnostic(
                Guid.NewGuid(),
                subsystem,
                severity,
                category,
                summary,
                detail,
                exception,
                exception?.ToString(),
                DateTimeOffset.UtcNow,
                visibility);
            DiagnosticRecorded?.Invoke(this, diagnostic);
        }
    }
}
