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
using Microsoft.Extensions.Logging;
using YouTubeMusicStreamer.Services.App.Persistence;

namespace YouTubeMusicStreamer.Services.App.Diagnostics;

public sealed class AppDiagnosticsService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ILogger<AppDiagnosticsService> logger) : IAppDiagnosticsService
{
    private const int MaxRecentDiagnostics = 200;
    private static readonly TimeSpan MaxDiagnosticAge = TimeSpan.FromDays(14);
    private static readonly TimeSpan DuplicateDiagnosticCooldown = TimeSpan.FromSeconds(30);
    private readonly Lock _lock = new();
    private readonly Lock _storageLock = new();
    private readonly Queue<AppDiagnostic> _diagnostics = [];
    private readonly Dictionary<string, DateTimeOffset> _lastDiagnosticByFingerprint = [];
    private bool _initialized;
    private bool _storageReady;

    public event EventHandler<AppDiagnostic>? DiagnosticRecorded;

    public IReadOnlyList<AppDiagnostic> RecentDiagnostics
    {
        get
        {
            lock (_lock)
            {
                EnsureLoaded();
                return _diagnostics.Reverse().ToList();
            }
        }
    }

    public async Task ClearAsync()
    {
        await EnsureStorageReadyAsync();

        lock (_lock)
        {
            EnsureLoaded();
            _diagnostics.Clear();
            _lastDiagnosticByFingerprint.Clear();
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        dbContext.AppDiagnostics.RemoveRange(dbContext.AppDiagnostics.AsTracking());
        await dbContext.SaveChangesAsync();
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

        lock (_lock)
        {
            EnsureLoaded();

            if (ShouldThrottleDuplicateDiagnostic(diagnostic))
            {
                return diagnostic;
            }

            _diagnostics.Enqueue(diagnostic);
            _lastDiagnosticByFingerprint[GetFingerprint(diagnostic)] = diagnostic.CreatedAtUtc;

            while (_diagnostics.Count > MaxRecentDiagnostics)
            {
                _diagnostics.Dequeue();
            }
        }

        PersistDiagnostic(diagnostic);

        DiagnosticRecorded?.Invoke(this, diagnostic);
        return diagnostic;
    }

    private void EnsureLoaded()
    {
        if (_initialized)
            return;

        try
        {
            EnsureStorageReady();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to initialize diagnostics storage");
            _diagnostics.Clear();
            _lastDiagnosticByFingerprint.Clear();
            _initialized = true;
            return;
        }

        Exception? lastException = null;

        for (var attempt = 0; attempt < 3 && !_initialized; attempt++)
        {
            try
            {
                using var dbContext = dbContextFactory.CreateDbContext();
                PurgeExpiredDiagnostics(dbContext);

                var diagnostics = dbContext.AppDiagnostics
                    .ToList()
                    .OrderByDescending(diagnostic => diagnostic.CreatedAtUtc)
                    .Take(MaxRecentDiagnostics)
                    .OrderBy(diagnostic => diagnostic.CreatedAtUtc);

                _diagnostics.Clear();
                _lastDiagnosticByFingerprint.Clear();

                foreach (var diagnostic in diagnostics)
                {
                    var loadedDiagnostic = new AppDiagnostic(
                        diagnostic.Id,
                        diagnostic.Subsystem,
                        diagnostic.Severity,
                        diagnostic.Category,
                        diagnostic.Summary,
                        diagnostic.Detail,
                        null,
                        diagnostic.ExceptionText,
                        diagnostic.CreatedAtUtc,
                        diagnostic.Visibility);

                    _diagnostics.Enqueue(loadedDiagnostic);
                    _lastDiagnosticByFingerprint[GetFingerprint(loadedDiagnostic)] = loadedDiagnostic.CreatedAtUtc;
                }

                _initialized = true;
            }
            catch (Exception ex)
            {
                lastException = ex;

                if (attempt < 2)
                {
                    Thread.Sleep(100);
                }
            }
        }

        if (!_initialized && lastException is not null)
        {
            logger.LogWarning(lastException, "Failed to load persisted diagnostics");
        }
    }

    private void PersistDiagnostic(AppDiagnostic diagnostic)
    {
        try
        {
            EnsureStorageReady();
            using var dbContext = dbContextFactory.CreateDbContext();
            PurgeExpiredDiagnostics(dbContext);

            dbContext.AppDiagnostics.Add(new AppDiagnosticEntity
            {
                Id = diagnostic.Id,
                Subsystem = diagnostic.Subsystem,
                Severity = diagnostic.Severity,
                Category = diagnostic.Category,
                Summary = diagnostic.Summary,
                Detail = diagnostic.Detail,
                ExceptionText = diagnostic.ExceptionDisplayText,
                CreatedAtUtc = diagnostic.CreatedAtUtc,
                Visibility = diagnostic.Visibility
            });
            dbContext.SaveChanges();

            var overflowIds = dbContext.AppDiagnostics
                .ToList()
                .OrderByDescending(entry => entry.CreatedAtUtc)
                .Skip(MaxRecentDiagnostics)
                .Select(entry => entry.Id)
                .ToList();

            if (overflowIds.Count == 0)
                return;

            var overflowEntries = dbContext.AppDiagnostics
                .AsTracking()
                .Where(entry => overflowIds.Contains(entry.Id))
                .ToList();

            dbContext.AppDiagnostics.RemoveRange(overflowEntries);
            dbContext.SaveChanges();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to persist diagnostic");
        }
    }

    private void PurgeExpiredDiagnostics(AppDbContext dbContext)
    {
        var cutoff = DateTimeOffset.UtcNow - MaxDiagnosticAge;
        var expiredEntries = dbContext.AppDiagnostics
            .AsTracking()
            .ToList()
            .Where(entry => entry.CreatedAtUtc < cutoff)
            .ToList();

        if (expiredEntries.Count == 0)
            return;

        dbContext.AppDiagnostics.RemoveRange(expiredEntries);
        dbContext.SaveChanges();
    }

    private void EnsureStorageReady()
    {
        if (_storageReady)
            return;

        lock (_storageLock)
        {
            if (_storageReady)
                return;

            using var dbContext = dbContextFactory.CreateDbContext();
            dbContext.Database.Migrate();
            _storageReady = true;
        }
    }

    private async Task EnsureStorageReadyAsync()
    {
        if (_storageReady)
            return;

        await Task.Run(EnsureStorageReady);
    }

    private bool ShouldThrottleDuplicateDiagnostic(AppDiagnostic diagnostic)
    {
        var fingerprint = GetFingerprint(diagnostic);

        return _lastDiagnosticByFingerprint.TryGetValue(fingerprint, out var lastRecordedAt)
               && diagnostic.CreatedAtUtc - lastRecordedAt < DuplicateDiagnosticCooldown;
    }

    private static string GetFingerprint(AppDiagnostic diagnostic) =>
        string.Join('|',
            diagnostic.Subsystem,
            diagnostic.Severity,
            diagnostic.Category,
            diagnostic.Visibility,
            diagnostic.Summary,
            diagnostic.Detail ?? string.Empty,
            diagnostic.ExceptionText ?? diagnostic.Exception?.GetType().FullName ?? string.Empty);
}
