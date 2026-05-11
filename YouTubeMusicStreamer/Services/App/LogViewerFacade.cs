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
using Microsoft.Extensions.Logging;
using YouTubeMusicStreamer.Services.App.Diagnostics;
using YouTubeMusicStreamer.Services.App.Persistence;
using YouTubeMusicStreamer.Services.Startup;
using YouTubeMusicStreamer.Services.Twitch;
using YouTubeMusicStreamer.Services.WebSocket;
using YouTubeMusicStreamer.Services.YouTube;

namespace YouTubeMusicStreamer.Services.App;

public sealed record SubsystemHealthSummary(
    string Name,
    string Status,
    string? Detail,
    string Tone);

public sealed class LogViewerFacade(
    SettingsService settingsService,
    IAppDiagnosticsService diagnosticsService,
    IStartupCoordinator startupCoordinator,
    ITwitchStatusSource twitchService,
    IYouTubeStatusSource youTubeService,
    IWidgetServerStatusSource webSocketService,
    IVersionStatusSource versionService,
    ILogViewerEnvironment environment,
    ILogger<LogViewerFacade> logger) : IDisposable
{
    private IDisposable? _logWatcherSubscription;
    private bool _initialized;

    public event EventHandler? StateChanged;

    public AppConfigurationSnapshot Configuration { get; private set; } = new(LogLevel.Warning);
    public IReadOnlyList<string> LogLines { get; private set; } = [];
    public IReadOnlyList<AppDiagnostic> RecentDiagnostics { get; private set; } = [];
    public IReadOnlyList<SubsystemHealthSummary> HealthSummaries { get; private set; } = [];

    public void Initialize()
    {
        if (_initialized)
            return;

        _initialized = true;
        Configuration = settingsService.GetAppConfiguration();
        RefreshLogLines(GetLogFilePath());
        RefreshDiagnostics();
        RefreshHealthSummaries();
        settingsService.AppConfigurationChanged += HandleAppConfigurationChanged;
        diagnosticsService.DiagnosticRecorded += HandleDiagnosticRecorded;
        startupCoordinator.HealthChanged += HandleStartupHealthChanged;
        twitchService.PropertyChanged += HandleServicePropertyChanged;
        youTubeService.PropertyChanged += HandleServicePropertyChanged;
        webSocketService.StateChanged += HandleWidgetServerStateChanged;
        versionService.OnChange += HandleVersionChanged;

        _logWatcherSubscription = environment.WatchLogFile(environment.LogFolderPath, environment.CurrentLogFileName, HandleFileSystemChanged);
    }

    public async Task SaveConfigurationAsync(AppConfigurationSnapshot snapshot)
    {
        await settingsService.SaveAppConfigurationAsync(snapshot);
    }

    public void OpenLogFolder()
    {
        environment.OpenFolder(environment.LogFolderPath);
    }

    public async Task CopyDiagnosticsSummaryAsync()
    {
        await environment.SetClipboardTextAsync(BuildDiagnosticsSummary());
    }

    public async Task ClearDiagnosticsAsync()
    {
        await diagnosticsService.ClearAsync();
        RefreshDiagnostics();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        settingsService.AppConfigurationChanged -= HandleAppConfigurationChanged;
        diagnosticsService.DiagnosticRecorded -= HandleDiagnosticRecorded;
        startupCoordinator.HealthChanged -= HandleStartupHealthChanged;
        twitchService.PropertyChanged -= HandleServicePropertyChanged;
        youTubeService.PropertyChanged -= HandleServicePropertyChanged;
        webSocketService.StateChanged -= HandleWidgetServerStateChanged;
        versionService.OnChange -= HandleVersionChanged;

        if (_logWatcherSubscription is not null)
        {
            _logWatcherSubscription.Dispose();
            _logWatcherSubscription = null;
        }

        _initialized = false;
        GC.SuppressFinalize(this);
    }

    private void HandleAppConfigurationChanged(AppConfigurationSnapshot snapshot)
    {
        Configuration = snapshot;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void HandleDiagnosticRecorded(object? sender, AppDiagnostic diagnostic)
    {
        RefreshDiagnostics();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void HandleStartupHealthChanged(object? sender, InstanceHealthState health)
    {
        RefreshHealthSummaries();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void HandleServicePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        RefreshHealthSummaries();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void HandleWidgetServerStateChanged(object? sender, WidgetServerState state)
    {
        RefreshHealthSummaries();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void HandleVersionChanged()
    {
        RefreshHealthSummaries();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void HandleFileSystemChanged(string fullPath)
    {
        if (!string.Equals(fullPath, GetLogFilePath(), StringComparison.OrdinalIgnoreCase))
            return;

        RefreshLogLines(fullPath);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshLogLines(string fullPath)
    {
        LogLines = ReadLogLines(fullPath).TakeLast(100).ToList();
    }

    private void RefreshDiagnostics()
    {
        RecentDiagnostics = diagnosticsService.RecentDiagnostics.Take(20).ToList();
    }

    private void RefreshHealthSummaries()
    {
        var startupIssue = startupCoordinator.CurrentIssues.LastOrDefault();
        var startupTone = startupCoordinator.CurrentHealth.State switch
        {
            InstanceRuntimeState.Ready => "success",
            InstanceRuntimeState.Starting => "info",
            InstanceRuntimeState.ShuttingDown => "warning",
            InstanceRuntimeState.Unhealthy => "danger",
            _ => "info"
        };

        var twitchTone = twitchService.SessionStatus switch
        {
            TwitchSessionStatus.Ready => "success",
            TwitchSessionStatus.Degraded => "warning",
            TwitchSessionStatus.Error => "danger",
            TwitchSessionStatus.LoggedOut or TwitchSessionStatus.AuthRequired or TwitchSessionStatus.NotConfigured => "muted",
            _ => "info"
        };

        var ytmTone = youTubeService.SessionState.ConnectionStatus switch
        {
            YtmConnectionStatus.Connected => "success",
            YtmConnectionStatus.Retrying or YtmConnectionStatus.Degraded => "warning",
            YtmConnectionStatus.Error => "danger",
            YtmConnectionStatus.Disconnected => "muted",
            _ => "info"
        };

        var widgetTone = webSocketService.State.Status switch
        {
            WidgetServerStatus.Running => "success",
            WidgetServerStatus.Degraded => "warning",
            WidgetServerStatus.Error => "danger",
            WidgetServerStatus.Stopped => "muted",
            _ => "info"
        };

        var audioTone = webSocketService.State.AudioState.Status switch
        {
            AudioCaptureStatus.Capturing => "success",
            AudioCaptureStatus.Error => "danger",
            AudioCaptureStatus.Disabled or AudioCaptureStatus.Stopped => "muted",
            _ => "info"
        };

        var updateTone = versionService.Status switch
        {
            VersionService.UpdateStatus.UpToDate => "success",
            VersionService.UpdateStatus.Available or VersionService.UpdateStatus.PendingRestart => "warning",
            VersionService.UpdateStatus.CheckFailed => "danger",
            VersionService.UpdateStatus.NotInstalled => "muted",
            _ => "info"
        };

        HealthSummaries =
        [
            new("Startup", startupCoordinator.CurrentHealth.StateHumanReadable, startupIssue?.Message ?? startupCoordinator.CurrentHealth.PhaseHumanReadable, startupTone),
            new("Twitch", twitchService.SessionStatus.ToString(), BuildTwitchHealthDetail(), twitchTone),
            new("YTMDesktop", youTubeService.SessionState.ConnectionStatus.ToString(), youTubeService.Error, ytmTone),
            new("Widget Server", webSocketService.State.Status.ToString(), webSocketService.State.ErrorMessage ?? $"Clients: {webSocketService.State.ConnectedClients}", widgetTone),
            new("Audio Capture", webSocketService.State.AudioState.Status.ToString(), webSocketService.State.AudioState.ErrorMessage ?? webSocketService.State.AudioState.DeviceId, audioTone),
            new("Updates", versionService.Status.ToString(), versionService.StatusText, updateTone)
        ];
    }

    private string BuildTwitchHealthDetail()
    {
        var broadcaster = string.IsNullOrWhiteSpace(twitchService.Username)
            ? twitchService.BroadcasterAccountId
            : twitchService.Username;
        var bot = twitchService.BotUsername;

        if (!string.IsNullOrWhiteSpace(broadcaster) && !string.IsNullOrWhiteSpace(bot))
            return $"Broadcaster: {broadcaster} | Bot: {bot}";

        if (!string.IsNullOrWhiteSpace(broadcaster))
            return $"Broadcaster: {broadcaster}";

        if (!string.IsNullOrWhiteSpace(bot))
            return $"Bot: {bot}";

        return "No Twitch account connected.";
    }

    private string GetLogFilePath() => Path.Combine(environment.LogFolderPath, environment.CurrentLogFileName);

    private string BuildDiagnosticsSummary()
    {
        var lines = new List<string>
        {
            $"Generated: {DateTimeOffset.Now:O}",
            $"Log level: {Configuration.LogLevel}",
            string.Empty,
            "Recent diagnostics:"
        };

        foreach (var diagnostic in RecentDiagnostics)
        {
            lines.Add(
                $"- [{diagnostic.CreatedAtUtc:O}] {diagnostic.Severity} {diagnostic.Subsystem}/{diagnostic.Category}: {diagnostic.Summary}{(string.IsNullOrWhiteSpace(diagnostic.Detail) ? string.Empty : $" ({diagnostic.Detail})")}");

            if (!string.IsNullOrWhiteSpace(diagnostic.ExceptionDisplayText))
            {
                lines.Add("  Exception:");
                lines.AddRange(diagnostic.ExceptionDisplayText
                    .Split(Environment.NewLine)
                    .Select(line => $"    {line}"));
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    private IReadOnlyList<string> ReadLogLines(string fullPath)
    {
        try
        {
            return environment.ReadLogLines(fullPath);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to read log file");
            return [];
        }
    }
}
