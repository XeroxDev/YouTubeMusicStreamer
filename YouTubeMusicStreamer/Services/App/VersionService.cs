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

#if !DEBUG
using Velopack.Sources;
#else
using Velopack.Locators;
#endif
using NuGet.Versioning;
using Velopack;
using YouTubeMusicStreamer.Utils;

namespace YouTubeMusicStreamer.Services.App;

public class VersionService
{
    public enum UpdateStatus
    {
        Pending,
        Checking,
        UpToDate,
        Available,
        PendingRestart,
        NotInstalled
    }

    private readonly UpdateManager _mgr;
    private DateTime _lastCheckTime = DateTime.MinValue;

    private UpdateInfo? _updateInfo;
    private bool _isDownloading;
    private bool _isApplying;

    public bool IsInstalled => _mgr.IsInstalled;
    public bool IsPortable => _mgr.IsPortable;
    public UpdateStatus Status { get; private set; } = UpdateStatus.Pending;
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
#if !DEBUG
        SettingsService settingsService
#endif
    )
    {
        var options = new UpdateOptions { AllowVersionDowngrade = true };
#if DEBUG
        var updatePath = Path.Combine(AppUtils.FilePath, "updates");
        _mgr = new UpdateManager(
            updatePath,
            options,
            new TestVelopackLocator(
                "YouTubeMusicStreamer",
                $"{GetInternalAppVersion()}-debug",
                updatePath)
        );
#else
        _mgr = new UpdateManager(
            new GithubSource(
                "https://github.com/XeroxDev/YouTubeMusicStreamer",
                null,
                settingsService.GetAppSettings().EnablePreRelease),
            options
        );
#endif
    }

    private static SemanticVersion GetInternalAppVersion()
    {
        var versionString = AppInfo.Current.VersionString;
        if (string.IsNullOrWhiteSpace(versionString))
            return new SemanticVersion(0, 0, 0);

        var parts = versionString.Split('.');
        int major = 0, minor = 0, patch = 0;
        if (parts.Length > 0 && int.TryParse(parts[0], out var m)) major = m;
        if (parts.Length > 1 && int.TryParse(parts[1], out var n)) minor = n;
        if (parts.Length > 2 && int.TryParse(parts[2], out var p)) patch = p;
        return new SemanticVersion(major, minor, patch);
    }

    public SemanticVersion GetAppVersion() => _mgr.CurrentVersion ?? GetInternalAppVersion();

    public async Task InitializeIfNeededAsync()
    {
        if (_lastCheckTime.AddHours(1) > DateTime.Now)
            return;
        _lastCheckTime = DateTime.Now;
        await CheckForUpdatesAsync();
    }

    public async Task CheckForUpdatesAsync()
    {
        if (!_mgr.IsInstalled)
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

        if (_mgr.UpdatePendingRestart is { } pendingAsset)
        {
            UpdateVersion = pendingAsset;
            Status = UpdateStatus.PendingRestart;
            Notify();
            return;
        }

        UpdateInfo? info;
        try
        {
            info = await _mgr.CheckForUpdatesAsync();
        }
        catch
        {
            Status = UpdateStatus.UpToDate;
            Notify();
            return;
        }

        if (info is null)
        {
            Status = UpdateStatus.UpToDate;
            Notify();
            return;
        }

        _updateInfo = info;
        UpdateVersion = info;
        Status = UpdateStatus.Available;
        Notify();
    }

    public async Task DownloadUpdatesAsync()
    {
        if (_isDownloading || _updateInfo is null)
            return;

        _isDownloading = true;
        Notify();

        try
        {
            await _mgr.DownloadUpdatesAsync(_updateInfo);
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

        _mgr.ApplyUpdatesAndRestart(_updateInfo);
        return Task.CompletedTask;
    }
}