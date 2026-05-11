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
using YouTubeMusicStreamer.Services.App.Diagnostics;

namespace YouTubeMusicStreamer.Tests.Services.App.Diagnostics;

public class DiagnosticReportBuilderTests
{
    [Fact]
    public void Write_Throws_WhenSummaryWasNotProvided()
    {
        var logger = new TestLogger<object>();
        var diagnostics = new TestDiagnosticsService();
        var builder = new DiagnosticReportBuilder<object>(logger, diagnostics, AppDiagnosticSubsystem.Logging);

        var exception = Assert.Throws<InvalidOperationException>(() => builder.Write());

        Assert.Equal("A diagnostic summary must be provided before calling Write().", exception.Message);
    }

    [Fact]
    public void Write_UsesWarningDefaultsAndRecordsDiagnostic_WhenWarningWasConfigured()
    {
        var logger = new TestLogger<object>();
        var diagnostics = new TestDiagnosticsService();
        var exception = new InvalidOperationException("Boom");

        var diagnostic = new DiagnosticReportBuilder<object>(logger, diagnostics, AppDiagnosticSubsystem.Updates)
            .Warning(AppDiagnosticCategory.Update, "Update check failed")
            .WithDetail("Network unavailable")
            .WithException(exception)
            .Toast()
            .Write();

        var logEntry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, logEntry.Level);
        Assert.Contains("Update check failed", logEntry.Message);
        Assert.Contains("Network unavailable", logEntry.Message);
        Assert.Same(exception, logEntry.Exception);

        Assert.Equal(AppDiagnosticSubsystem.Updates, diagnostic.Subsystem);
        Assert.Equal(AppDiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal(AppDiagnosticCategory.Update, diagnostic.Category);
        Assert.Equal("Update check failed", diagnostic.Summary);
        Assert.Equal("Network unavailable", diagnostic.Detail);
        Assert.Same(exception, diagnostic.Exception);
        Assert.Equal(AppDiagnosticVisibility.Toast, diagnostic.Visibility);
        Assert.Same(diagnostic, Assert.Single(diagnostics.RecentDiagnostics));
    }

    [Fact]
    public void Write_UsesExplicitLogLevel_WhenLogLevelWasOverridden()
    {
        var logger = new TestLogger<object>();
        var diagnostics = new TestDiagnosticsService();

        _ = new DiagnosticReportBuilder<object>(logger, diagnostics, AppDiagnosticSubsystem.Settings)
            .Info(AppDiagnosticCategory.Persistence, "Settings adopted")
            .WithLogLevel(LogLevel.Error)
            .DiagnosticsOnly()
            .Write();

        var logEntry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, logEntry.Level);
        Assert.Contains("Settings adopted", logEntry.Message);
    }

    [Fact]
    public void Write_UsesErrorDefaultsAndDiagnosticsOnlyVisibility_WhenErrorWasConfiguredWithoutOverrides()
    {
        var logger = new TestLogger<object>();
        var diagnostics = new TestDiagnosticsService();

        var diagnostic = new DiagnosticReportBuilder<object>(logger, diagnostics, AppDiagnosticSubsystem.YouTube)
            .Error(AppDiagnosticCategory.Connectivity, "Socket disconnected")
            .Write();

        var logEntry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, logEntry.Level);
        Assert.Equal("Socket disconnected", logEntry.Message);
        Assert.Null(logEntry.Exception);

        Assert.Equal(AppDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal(AppDiagnosticVisibility.DiagnosticsOnly, diagnostic.Visibility);
        Assert.Equal(AppDiagnosticCategory.Connectivity, diagnostic.Category);
        Assert.Equal("Socket disconnected", diagnostic.Summary);
        Assert.Null(diagnostic.Detail);
        Assert.Null(diagnostic.Exception);
    }

    [Fact]
    public void Write_UsesLatestSeverityConfiguration_WhenSeverityWasChangedMultipleTimes()
    {
        var logger = new TestLogger<object>();
        var diagnostics = new TestDiagnosticsService();

        var diagnostic = new DiagnosticReportBuilder<object>(logger, diagnostics, AppDiagnosticSubsystem.Settings)
            .Info(AppDiagnosticCategory.Persistence, "Initial")
            .Warning(AppDiagnosticCategory.InternalFault, "Final warning")
            .Write();

        var logEntry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, logEntry.Level);
        Assert.Equal("Final warning", logEntry.Message);

        Assert.Equal(AppDiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal(AppDiagnosticCategory.InternalFault, diagnostic.Category);
        Assert.Equal("Final warning", diagnostic.Summary);
    }

    [Fact]
    public void Write_UsesLatestVisibilityHelper_WhenVisibilityWasChangedMultipleTimes()
    {
        var logger = new TestLogger<object>();
        var diagnostics = new TestDiagnosticsService();

        var diagnostic = new DiagnosticReportBuilder<object>(logger, diagnostics, AppDiagnosticSubsystem.Twitch)
            .Warning(AppDiagnosticCategory.Auth, "Auth warning")
            .Toast()
            .StatusOnly()
            .Write();

        Assert.Equal(AppDiagnosticVisibility.StatusOnly, diagnostic.Visibility);
    }

    [Fact]
    public void Write_LogsSummaryOnly_WhenDetailWasExplicitlyCleared()
    {
        var logger = new TestLogger<object>();
        var diagnostics = new TestDiagnosticsService();

        var diagnostic = new DiagnosticReportBuilder<object>(logger, diagnostics, AppDiagnosticSubsystem.Logging)
            .Info(AppDiagnosticCategory.InternalFault, "Single line")
            .WithDetail("Extra detail")
            .WithDetail(null)
            .Write();

        var logEntry = Assert.Single(logger.Entries);
        Assert.Equal("Single line", logEntry.Message);
        Assert.Null(diagnostic.Detail);
    }

    private sealed class TestDiagnosticsService : IAppDiagnosticsService
    {
        private readonly List<AppDiagnostic> _diagnostics = [];

        public event EventHandler<AppDiagnostic>? DiagnosticRecorded;

        public IReadOnlyList<AppDiagnostic> RecentDiagnostics => _diagnostics;

        public Task ClearAsync()
        {
            _diagnostics.Clear();
            return Task.CompletedTask;
        }

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

            _diagnostics.Add(diagnostic);
            DiagnosticRecorded?.Invoke(this, diagnostic);
            return diagnostic;
        }
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
        }
    }

    private sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);
}
