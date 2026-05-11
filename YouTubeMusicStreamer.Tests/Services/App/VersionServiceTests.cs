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

using Microsoft.Extensions.Logging.Abstractions;
using NuGet.Versioning;
using Velopack;
using YouTubeMusicStreamer.Services.App;
using YouTubeMusicStreamer.Services.App.Diagnostics;
using YouTubeMusicStreamer.Tests.TestSupport;

namespace YouTubeMusicStreamer.Tests.Services.App;

public class VersionServiceTests
{
    [Fact]
    public void GetAppVersion_ReturnsInternalVersion_WhenUpdateManagerHasNoCurrentVersion()
    {
        using var fixture = CreateFixture();

        var version = fixture.Service.GetAppVersion();

        Assert.Equal(new SemanticVersion(1, 2, 3), version);
        Assert.Equal(1, fixture.Factory.CreateCallCount);
    }

    [Fact]
    public void GetAppVersion_ReturnsInstalledVersion_WhenUpdateManagerHasCurrentVersion()
    {
        using var fixture = CreateFixture();
        fixture.Manager.CurrentVersion = new SemanticVersion(9, 9, 9);

        var version = fixture.Service.GetAppVersion();

        Assert.Equal(new SemanticVersion(9, 9, 9), version);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_SetsNotInstalled_WhenUpdateManagerIsNotInstalled()
    {
        using var fixture = CreateFixture();
        fixture.Manager.IsInstalled = false;
        var changes = TrackChanges(fixture.Service);

        await fixture.Service.CheckForUpdatesAsync();

        Assert.True(fixture.Service.WasLastCheckAttempted);
        Assert.Equal(VersionService.UpdateStatus.NotInstalled, fixture.Service.Status);
        Assert.Equal("Code version—updates disabled", fixture.Service.StatusText);
        Assert.Single(changes);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_SetsPendingRestart_WhenPreparedUpdateAlreadyExists()
    {
        using var fixture = CreateFixture();
        fixture.Manager.UpdatePendingRestart = CreateAsset("2.0.0");

        await fixture.Service.CheckForUpdatesAsync();

        Assert.Equal(VersionService.UpdateStatus.PendingRestart, fixture.Service.Status);
        Assert.Equal("Update & Restart", fixture.Service.ButtonText);
        Assert.Equal("2.0.0", fixture.Service.UpdateVersion!.Version.ToNormalizedString());
    }

    [Fact]
    public async Task ApplyUpdatesAndRestartAsync_CallsUpdateManager_WhenPendingRestartWasDetectedFromExistingDownload()
    {
        using var fixture = CreateFixture();
        fixture.Manager.UpdatePendingRestart = CreateAsset("2.0.0");
        await fixture.Service.CheckForUpdatesAsync();

        await fixture.Service.ApplyUpdatesAndRestartAsync();

        Assert.Equal(1, fixture.Manager.ApplyCallCount);
        Assert.True(fixture.Service.IsBusy);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_SetsUpToDate_WhenNoUpdateIsAvailable()
    {
        using var fixture = CreateFixture();

        await fixture.Service.CheckForUpdatesAsync();

        Assert.Equal(VersionService.UpdateStatus.UpToDate, fixture.Service.Status);
        Assert.Equal("Everything up to date", fixture.Service.StatusText);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_ClearsStaleUpdateVersion_WhenNoUpdateIsAvailableAfterPreviousAvailability()
    {
        using var fixture = CreateFixture();
        fixture.Manager.CheckResult = CreateUpdateInfo("2.0.0");
        await fixture.Service.CheckForUpdatesAsync();

        fixture.Manager.CheckResult = null;
        await fixture.Service.CheckForUpdatesAsync();

        Assert.Equal(VersionService.UpdateStatus.UpToDate, fixture.Service.Status);
        Assert.Null(fixture.Service.UpdateVersion);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_SetsAvailable_WhenUpdateWasFound()
    {
        using var fixture = CreateFixture();
        fixture.Manager.CheckResult = CreateUpdateInfo("2.0.0");

        await fixture.Service.CheckForUpdatesAsync();

        Assert.Equal(VersionService.UpdateStatus.Available, fixture.Service.Status);
        Assert.Equal("Download Update", fixture.Service.ButtonText);
        Assert.Equal("2.0.0", fixture.Service.UpdateVersion!.Version.ToNormalizedString());
        Assert.Contains("v2.0.0", fixture.Service.StatusText);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_RecordsFailure_WhenUpdateCheckThrows()
    {
        using var fixture = CreateFixture();
        fixture.Manager.CheckException = new InvalidOperationException("No network");

        await fixture.Service.CheckForUpdatesAsync();

        Assert.Equal(VersionService.UpdateStatus.CheckFailed, fixture.Service.Status);
        Assert.Equal("Update check failed", fixture.Service.StatusText);
        Assert.IsType<InvalidOperationException>(fixture.Service.LastCheckError);
        var diagnostic = Assert.Single(fixture.Diagnostics.RecentDiagnostics);
        Assert.Equal(AppDiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal(AppDiagnosticCategory.Update, diagnostic.Category);
        Assert.Equal("No network", diagnostic.Detail);
    }

    [Fact]
    public async Task DownloadUpdatesAsync_SetsPendingRestart_WhenDownloadSucceeds()
    {
        using var fixture = CreateFixture();
        fixture.Manager.CheckResult = CreateUpdateInfo("2.0.0");
        await fixture.Service.CheckForUpdatesAsync();

        await fixture.Service.DownloadUpdatesAsync();

        Assert.Equal(VersionService.UpdateStatus.PendingRestart, fixture.Service.Status);
        Assert.Equal(1, fixture.Manager.DownloadCallCount);
        Assert.False(fixture.Service.IsBusy);
        Assert.False(fixture.Service.SpinnerVisible);
    }

    [Fact]
    public async Task DownloadUpdatesAsync_ResetsBusyStateAndPreservesAvailability_WhenDownloadThrows()
    {
        using var fixture = CreateFixture();
        fixture.Manager.CheckResult = CreateUpdateInfo("2.0.0");
        fixture.Manager.DownloadException = new IOException("disk full");
        await fixture.Service.CheckForUpdatesAsync();

        var ex = await Assert.ThrowsAsync<IOException>(() => fixture.Service.DownloadUpdatesAsync());

        Assert.Equal("disk full", ex.Message);
        Assert.Equal(VersionService.UpdateStatus.Available, fixture.Service.Status);
        Assert.False(fixture.Service.IsBusy);
        Assert.False(fixture.Service.SpinnerVisible);
        Assert.Equal(1, fixture.Manager.DownloadCallCount);
    }

    [Fact]
    public async Task DownloadUpdatesAsync_DoesNothing_WhenNoPendingUpdateExists()
    {
        using var fixture = CreateFixture();

        await fixture.Service.DownloadUpdatesAsync();

        Assert.Equal(0, fixture.Manager.DownloadCallCount);
        Assert.False(fixture.Service.IsBusy);
    }

    [Fact]
    public async Task ApplyUpdatesAndRestartAsync_CallsUpdateManager_WhenUpdateIsPending()
    {
        using var fixture = CreateFixture();
        fixture.Manager.CheckResult = CreateUpdateInfo("2.0.0");
        await fixture.Service.CheckForUpdatesAsync();

        await fixture.Service.ApplyUpdatesAndRestartAsync();

        Assert.Equal(1, fixture.Manager.ApplyCallCount);
        Assert.True(fixture.Service.IsBusy);
        Assert.True(fixture.Service.SpinnerVisible);
    }

    [Fact]
    public async Task ApplyUpdatesAndRestartAsync_DoesNotReenter_WhenApplyAlreadyInProgress()
    {
        using var fixture = CreateFixture();
        fixture.Manager.CheckResult = CreateUpdateInfo("2.0.0");
        await fixture.Service.CheckForUpdatesAsync();

        await fixture.Service.ApplyUpdatesAndRestartAsync();
        await fixture.Service.ApplyUpdatesAndRestartAsync();

        Assert.Equal(1, fixture.Manager.ApplyCallCount);
        Assert.True(fixture.Service.IsBusy);
    }

    [Fact]
    public async Task ApplyUpdatesAndRestartAsync_DoesNothing_WhenNoPendingUpdateExists()
    {
        using var fixture = CreateFixture();

        await fixture.Service.ApplyUpdatesAndRestartAsync();

        Assert.Equal(0, fixture.Manager.ApplyCallCount);
        Assert.False(fixture.Service.IsBusy);
    }

    [Fact]
    public async Task InitializeIfNeededAsync_SkipsSecondCheck_WhenCheckWasRecentlyPerformed()
    {
        using var fixture = CreateFixture();

        await fixture.Service.InitializeIfNeededAsync();
        await fixture.Service.InitializeIfNeededAsync();

        Assert.Equal(1, fixture.Manager.CheckCallCount);
    }

    [Fact]
    public async Task SimulateUpdateCheckFailureAsync_SetsFailureStateWithoutManagerCall()
    {
        using var fixture = CreateFixture();

        await fixture.Service.SimulateUpdateCheckFailureAsync("Synthetic failure");

        Assert.Equal(VersionService.UpdateStatus.CheckFailed, fixture.Service.Status);
        Assert.Equal(0, fixture.Manager.CheckCallCount);
        Assert.Equal("Synthetic failure", fixture.Service.LastCheckError!.Message);
    }

    private static VersionServiceFixture CreateFixture()
    {
        var diagnostics = new RecordingDiagnosticsService();
        var manager = new FakeAppUpdateManager();
        var factory = new FakeAppUpdateManagerFactory(manager);
        var service = new VersionService(
            NullLogger<VersionService>.Instance,
            diagnostics,
            new FakeAppVersionSource(new SemanticVersion(1, 2, 3)),
            factory);

        return new VersionServiceFixture(service, manager, factory, diagnostics);
    }

    private static List<int> TrackChanges(VersionService service)
    {
        var changes = new List<int>();
        service.OnChange += () => changes.Add(changes.Count);
        return changes;
    }

    private static VelopackAsset CreateAsset(string version) =>
        new()
        {
            PackageId = "YouTubeMusicStreamer",
            Version = SemanticVersion.Parse(version),
            FileName = $"YouTubeMusicStreamer-{version}-full.nupkg",
            NotesMarkdown = string.Empty,
            NotesHTML = string.Empty
        };

    private static UpdateInfo CreateUpdateInfo(string version) =>
        new(CreateAsset(version), false, null!, []);

    private sealed class VersionServiceFixture(
        VersionService service,
        FakeAppUpdateManager manager,
        FakeAppUpdateManagerFactory factory,
        RecordingDiagnosticsService diagnostics) : IDisposable
    {
        public VersionService Service { get; } = service;
        public FakeAppUpdateManager Manager { get; } = manager;
        public FakeAppUpdateManagerFactory Factory { get; } = factory;
        public RecordingDiagnosticsService Diagnostics { get; } = diagnostics;

        public void Dispose() { }
    }

    private sealed class FakeAppVersionSource(SemanticVersion version) : IAppVersionSource
    {
        public SemanticVersion GetInternalAppVersion() => version;
    }

    private sealed class FakeAppUpdateManagerFactory(FakeAppUpdateManager manager) : IAppUpdateManagerFactory
    {
        public int CreateCallCount { get; private set; }

        public IAppUpdateManager Create(SemanticVersion internalVersion)
        {
            CreateCallCount++;
            return manager;
        }
    }

    private sealed class FakeAppUpdateManager : IAppUpdateManager
    {
        public bool IsInstalled { get; set; } = true;
        public bool IsPortable { get; set; }
        public SemanticVersion? CurrentVersion { get; set; }
        public VelopackAsset? UpdatePendingRestart { get; set; }
        public UpdateInfo? CheckResult { get; set; }
        public Exception? CheckException { get; set; }
        public Exception? DownloadException { get; set; }
        public int CheckCallCount { get; private set; }
        public int DownloadCallCount { get; private set; }
        public int ApplyCallCount { get; private set; }

        public Task<UpdateInfo?> CheckForUpdatesAsync()
        {
            CheckCallCount++;
            return CheckException is not null
                ? Task.FromException<UpdateInfo?>(CheckException)
                : Task.FromResult(CheckResult);
        }

        public Task DownloadUpdatesAsync(UpdateInfo updateInfo)
        {
            DownloadCallCount++;
            if (DownloadException is not null)
                return Task.FromException(DownloadException);

            return Task.CompletedTask;
        }

        public void ApplyUpdatesAndRestart(UpdateInfo updateInfo)
        {
            ApplyCallCount++;
        }
    }
}
