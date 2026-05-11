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
using NuGet.Versioning;
using Velopack;
using YouTubeMusicStreamer.Services.App.Diagnostics;

namespace YouTubeMusicStreamer.Services.App;

public class VersionService : IVersionStatusSource
{
    public enum UpdateStatus
    {
        Pending,
        Checking,
        UpToDate,
        CheckFailed,
        Available,
        PendingRestart,
        NotInstalled
    }

    private IAppUpdateManager? _mgr;
    private DateTime _lastCheckTime = DateTime.MinValue;

    private UpdateInfo? _updateInfo;
    private bool _isDownloading;
    private bool _isApplying;
    private readonly ILogger<VersionService> _logger;
    private readonly IAppDiagnosticsService _diagnosticsService;
    private readonly IAppVersionSource _appVersionSource;
    private readonly IAppUpdateManagerFactory _updateManagerFactory;

    public bool IsInstalled => GetUpdateManager().IsInstalled;
    public bool IsPortable => GetUpdateManager().IsPortable;
    public UpdateStatus Status { get; private set; } = UpdateStatus.Pending;
    public bool WasLastCheckAttempted { get; private set; }
    public Exception? LastCheckError { get; private set; }
    public bool IsBusy => _isDownloading || _isApplying;
    public bool SpinnerVisible => IsBusy || Status == UpdateStatus.Checking;

    public string StatusText
    {
        get
        {
            if (_isDownloading)
                return "Downloading…";
            if (_isApplying)
                return "Applying update…";
            return Status switch
            {
                UpdateStatus.Pending or UpdateStatus.Checking => "Checking for updates…",
                UpdateStatus.UpToDate => "Everything up to date",
                UpdateStatus.CheckFailed => "Update check failed",
                UpdateStatus.Available => $"New update available—v{UpdateVersion?.Version}",
                UpdateStatus.PendingRestart => "Update downloaded, awaiting restart",
                UpdateStatus.NotInstalled => "Code version—updates disabled",
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }

    public string ButtonText => Status switch
    {
        UpdateStatus.Available => "Download Update",
        UpdateStatus.PendingRestart => "Update & Restart",
        _ => "Check for Updates"
    };

    public bool ButtonDisabled => IsBusy || Status == UpdateStatus.Checking;

    public Func<Task> ButtonCallback => Status switch
    {
        UpdateStatus.Available => DownloadUpdatesAsync,
        UpdateStatus.PendingRestart => ApplyUpdatesAndRestartAsync,
        _ => CheckForUpdatesAsync
    };

    public VelopackAsset? UpdateVersion { get; private set; }

    public event Action? OnChange;
    private void Notify() => OnChange?.Invoke();

    public VersionService(
        ILogger<VersionService> logger,
        IAppDiagnosticsService diagnosticsService,
        IAppVersionSource appVersionSource,
        IAppUpdateManagerFactory updateManagerFactory)
    {
        _logger = logger;
        _diagnosticsService = diagnosticsService;
        _appVersionSource = appVersionSource;
        _updateManagerFactory = updateManagerFactory;
    }

    private IAppUpdateManager GetUpdateManager()
    {
        if (_mgr is not null)
            return _mgr;

        _mgr = _updateManagerFactory.Create(_appVersionSource.GetInternalAppVersion());
        return _mgr;
    }

    public SemanticVersion GetAppVersion() => GetUpdateManager().CurrentVersion ?? _appVersionSource.GetInternalAppVersion();

    public async Task InitializeIfNeededAsync()
    {
        if (_lastCheckTime.AddHours(1) > DateTime.Now)
            return;
        _lastCheckTime = DateTime.Now;
        await CheckForUpdatesAsync();
    }

    public async Task CheckForUpdatesAsync()
    {
        WasLastCheckAttempted = true;
        LastCheckError = null;

        var updateManager = GetUpdateManager();

        if (!updateManager.IsInstalled)
        {
            Status = UpdateStatus.NotInstalled;
            Notify();
            return;
        }

        if (Status == UpdateStatus.Checking)
            return;

        Status = UpdateStatus.Checking;
        Notify();

        await Task.Delay(1000);

        if (updateManager.UpdatePendingRestart is { } pendingAsset)
        {
            _updateInfo = new UpdateInfo(pendingAsset, false, null!, []);
            UpdateVersion = pendingAsset;
            Status = UpdateStatus.PendingRestart;
            Notify();
            return;
        }

        UpdateInfo? info;
        try
        {
            info = await updateManager.CheckForUpdatesAsync();
        }
        catch (Exception ex)
        {
            ApplyUpdateCheckFailure(ex);
            return;
        }

        if (info is null)
        {
            _updateInfo = null;
            UpdateVersion = null;
            Status = UpdateStatus.UpToDate;
            Notify();
            return;
        }

        _updateInfo = info;
        UpdateVersion = info;
        Status = UpdateStatus.Available;
        Notify();
    }

    public Task SimulateUpdateCheckFailureAsync(string? detail = null)
    {
        var exception = new InvalidOperationException(detail ?? "Debug-simulated update check failure.");
        ApplyUpdateCheckFailure(exception);
        return Task.CompletedTask;
    }

    public async Task DownloadUpdatesAsync()
    {
        if (_isDownloading || _updateInfo is null)
            return;

        _isDownloading = true;
        Notify();

        try
        {
            await GetUpdateManager().DownloadUpdatesAsync(_updateInfo);
            Status = UpdateStatus.PendingRestart;
            Notify();
        }
        finally
        {
            _isDownloading = false;
            Notify();
        }
    }

    public Task ApplyUpdatesAndRestartAsync()
    {
        if (_isApplying || _updateInfo is null)
            return Task.CompletedTask;

        _isApplying = true;
        Notify();

        GetUpdateManager().ApplyUpdatesAndRestart(_updateInfo);
        return Task.CompletedTask;
    }

    private void ApplyUpdateCheckFailure(Exception ex)
    {
        LastCheckError = ex;
        _updateInfo = null;
        UpdateVersion = null;
        _logger.Diagnostic(_diagnosticsService, AppDiagnosticSubsystem.Updates)
            .Warning(AppDiagnosticCategory.Update, "Update check failed")
            .WithLogLevel(LogLevel.Error)
            .WithDetail(ex.Message)
            .WithException(ex)
            .DiagnosticsOnly()
            .Write();
        Status = UpdateStatus.CheckFailed;
        Notify();
    }
}
