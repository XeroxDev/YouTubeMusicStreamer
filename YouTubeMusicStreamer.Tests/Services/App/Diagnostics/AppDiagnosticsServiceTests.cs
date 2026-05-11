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

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using YouTubeMusicStreamer.Services.App.Diagnostics;
using YouTubeMusicStreamer.Services.App.Persistence;
using YouTubeMusicStreamer.Tests.TestSupport;

namespace YouTubeMusicStreamer.Tests.Services.App.Diagnostics;

public sealed class AppDiagnosticsServiceTests
{
    [Fact]
    public async Task Record_PersistsDiagnosticAndRaisesEvent_WhenDiagnosticIsNew()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        var service = CreateService(harness.DatabaseFilePath);
        AppDiagnostic? raised = null;
        service.DiagnosticRecorded += (_, diagnostic) => raised = diagnostic;

        var recorded = service.Record(
            AppDiagnosticSubsystem.Commands,
            AppDiagnosticSeverity.Warning,
            AppDiagnosticCategory.Configuration,
            "Command config issue",
            "detail",
            visibility: AppDiagnosticVisibility.Toast);

        Assert.NotNull(raised);
        Assert.Equal(recorded.Id, raised!.Id);

        var recent = service.RecentDiagnostics;
        var diagnostic = Assert.Single(recent);
        Assert.Equal("Command config issue", diagnostic.Summary);
        Assert.Equal("detail", diagnostic.Detail);
        Assert.Equal(AppDiagnosticVisibility.Toast, diagnostic.Visibility);

        await using var db = new FileBackedSqliteDbContextFactory(harness.DatabaseFilePath).CreateDbContext();
        var persisted = Assert.Single(await db.AppDiagnostics.ToListAsync());
        Assert.Equal(recorded.Id, persisted.Id);
        Assert.Equal("Command config issue", persisted.Summary);
    }

    [Fact]
    public async Task Record_ThrottlesDuplicateDiagnostic_WhenRepeatedWithinCooldown()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        var service = CreateService(harness.DatabaseFilePath);
        var raisedCount = 0;
        service.DiagnosticRecorded += (_, _) => raisedCount++;

        service.Record(
            AppDiagnosticSubsystem.Settings,
            AppDiagnosticSeverity.Error,
            AppDiagnosticCategory.Persistence,
            "Duplicate issue",
            "same detail");

        service.Record(
            AppDiagnosticSubsystem.Settings,
            AppDiagnosticSeverity.Error,
            AppDiagnosticCategory.Persistence,
            "Duplicate issue",
            "same detail");

        Assert.Single(service.RecentDiagnostics);
        Assert.Equal(1, raisedCount);

        await using var db = new FileBackedSqliteDbContextFactory(harness.DatabaseFilePath).CreateDbContext();
        Assert.Equal(1, await db.AppDiagnostics.CountAsync());
    }

    [Fact]
    public async Task RecentDiagnostics_LoadsPersistedDiagnosticsAndPurgesExpiredRows()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        await SeedDiagnosticsAsync(
            harness.DatabaseFilePath,
            CreateEntity("fresh", DateTimeOffset.UtcNow.AddDays(-1)),
            CreateEntity("expired", DateTimeOffset.UtcNow.AddDays(-20)));

        var service = CreateService(harness.DatabaseFilePath);

        var recent = service.RecentDiagnostics;

        var diagnostic = Assert.Single(recent);
        Assert.Equal("fresh", diagnostic.Summary);

        await using var db = new FileBackedSqliteDbContextFactory(harness.DatabaseFilePath).CreateDbContext();
        var persisted = await db.AppDiagnostics.Select(x => x.Summary).ToListAsync();
        Assert.Equal(["fresh"], persisted);
    }

    [Fact]
    public async Task ClearAsync_RemovesCachedAndPersistedDiagnostics()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        var service = CreateService(harness.DatabaseFilePath);

        service.Record(
            AppDiagnosticSubsystem.Startup,
            AppDiagnosticSeverity.Info,
            AppDiagnosticCategory.Startup,
            "startup");

        await service.ClearAsync();

        Assert.Empty(service.RecentDiagnostics);

        await using var db = new FileBackedSqliteDbContextFactory(harness.DatabaseFilePath).CreateDbContext();
        Assert.Equal(0, await db.AppDiagnostics.CountAsync());
    }

    [Fact]
    public async Task Record_TrimsPersistedAndCachedDiagnostics_WhenCountExceedsLimit()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        var service = CreateService(harness.DatabaseFilePath);

        for (var i = 0; i < 205; i++)
        {
            service.Record(
                AppDiagnosticSubsystem.Logging,
                AppDiagnosticSeverity.Info,
                AppDiagnosticCategory.InternalFault,
                $"diagnostic-{i}");
            await Task.Delay(35);
        }

        var recent = service.RecentDiagnostics;
        Assert.Equal(200, recent.Count);
        Assert.Equal("diagnostic-204", recent[0].Summary);
        Assert.Equal("diagnostic-5", recent[^1].Summary);

        await using var db = new FileBackedSqliteDbContextFactory(harness.DatabaseFilePath).CreateDbContext();
        Assert.Equal(200, await db.AppDiagnostics.CountAsync());
        Assert.DoesNotContain(await db.AppDiagnostics.Select(x => x.Summary).ToListAsync(), x => x == "diagnostic-0");
    }

    [Fact]
    public void Record_KeepsDiagnosticInMemoryAndRaisesEvent_WhenPersistenceFails()
    {
        var service = new AppDiagnosticsService(new ThrowingDiagnosticsDbContextFactory(), NullLogger<AppDiagnosticsService>.Instance);
        AppDiagnostic? raised = null;
        service.DiagnosticRecorded += (_, diagnostic) => raised = diagnostic;

        var recorded = service.Record(
            AppDiagnosticSubsystem.Settings,
            AppDiagnosticSeverity.Error,
            AppDiagnosticCategory.Persistence,
            "db locked",
            "write failed");

        Assert.NotNull(raised);
        Assert.Equal(recorded.Id, raised!.Id);
        var recent = Assert.Single(service.RecentDiagnostics);
        Assert.Equal("db locked", recent.Summary);
        Assert.Equal("write failed", recent.Detail);
    }

    [Fact]
    public async Task Record_DoesNotThrottle_WhenDetailDiffers()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        var service = CreateService(harness.DatabaseFilePath);

        service.Record(
            AppDiagnosticSubsystem.Settings,
            AppDiagnosticSeverity.Error,
            AppDiagnosticCategory.Persistence,
            "Duplicate issue",
            "first");

        service.Record(
            AppDiagnosticSubsystem.Settings,
            AppDiagnosticSeverity.Error,
            AppDiagnosticCategory.Persistence,
            "Duplicate issue",
            "second");

        Assert.Equal(2, service.RecentDiagnostics.Count);
    }

    private static AppDiagnosticsService CreateService(string databaseFilePath) =>
        new(new FileBackedSqliteDbContextFactory(databaseFilePath), NullLogger<AppDiagnosticsService>.Instance);

    private static async Task SeedDiagnosticsAsync(string databaseFilePath, params AppDiagnosticEntity[] entities)
    {
        await using var db = new FileBackedSqliteDbContextFactory(databaseFilePath).CreateDbContext();
        await db.Database.MigrateAsync();
        await db.AppDiagnostics.AddRangeAsync(entities);
        await db.SaveChangesAsync();
    }

    private static AppDiagnosticEntity CreateEntity(string summary, DateTimeOffset createdAtUtc) => new()
    {
        Id = Guid.NewGuid(),
        Subsystem = AppDiagnosticSubsystem.Settings,
        Severity = AppDiagnosticSeverity.Warning,
        Category = AppDiagnosticCategory.Persistence,
        Summary = summary,
        Detail = null,
        ExceptionText = null,
        CreatedAtUtc = createdAtUtc,
        Visibility = AppDiagnosticVisibility.DiagnosticsOnly
    };

    private sealed class ThrowingDiagnosticsDbContextFactory : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => throw new NotSupportedException("db unavailable");

        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("db unavailable");
    }
}
