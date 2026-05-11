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
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using YouTubeMusicStreamer.Services.App;
using YouTubeMusicStreamer.Services.App.Diagnostics;
using YouTubeMusicStreamer.Services.App.Persistence;

namespace YouTubeMusicStreamer.Tests.TestSupport;

internal static class SettingsServiceTestSupport
{
    public static SettingsService CreateWithThrowingDb(
        RecordingDiagnosticsService? diagnostics = null,
        YouTubeSettingsSnapshot? youTubeSettings = null,
        TwitchSettingsSnapshot? twitchSettings = null,
        IReadOnlyDictionary<string, CommandConfigurationSnapshot>? commandConfigurations = null)
    {
        diagnostics ??= new RecordingDiagnosticsService();
        var settings = new SettingsService(
            NullLogger<SettingsService>.Instance,
            new ThrowingDbContextFactory(),
            diagnostics,
            new TestAppPathProvider(),
            new AssemblyAppIdentitySource());

        ApplyCachedState(settings, youTubeSettings, twitchSettings, commandConfigurations, markInitialized: true);
        return settings;
    }

    public static SettingsService CreateWithInMemoryDb(
        InMemorySqliteDbContextFactory dbFactory,
        RecordingDiagnosticsService? diagnostics = null,
        YouTubeSettingsSnapshot? youTubeSettings = null,
        TwitchSettingsSnapshot? twitchSettings = null,
        IReadOnlyDictionary<string, CommandConfigurationSnapshot>? commandConfigurations = null)
    {
        diagnostics ??= new RecordingDiagnosticsService();
        var settings = new SettingsService(
            NullLogger<SettingsService>.Instance,
            dbFactory,
            diagnostics,
            new TestAppPathProvider(),
            new AssemblyAppIdentitySource());

        ApplyCachedState(settings, youTubeSettings, twitchSettings, commandConfigurations, markInitialized: true);
        return settings;
    }

    public static void SetPrivateField<T>(object instance, string fieldName, T value)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException($"Field '{fieldName}' was not found on '{instance.GetType().Name}'.");

        field.SetValue(instance, value);
    }

    public static void SetAutoProperty<T>(object instance, string propertyName, T value)
    {
        var field = instance.GetType().GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException($"Backing field for '{propertyName}' was not found on '{instance.GetType().Name}'.");

        field.SetValue(instance, value);
    }

    private static void ApplyCachedState(
        SettingsService settings,
        YouTubeSettingsSnapshot? youTubeSettings,
        TwitchSettingsSnapshot? twitchSettings,
        IReadOnlyDictionary<string, CommandConfigurationSnapshot>? commandConfigurations,
        bool markInitialized = false)
    {
        if (markInitialized)
            SetPrivateField(settings, "_isInitialized", true);

        if (youTubeSettings is not null)
            SetPrivateField(settings, "_youTubeSettings", youTubeSettings);

        if (twitchSettings is not null)
            SetPrivateField(settings, "_twitchSettings", twitchSettings);

        if (commandConfigurations is not null)
        {
            SetPrivateField(
                settings,
                "_commandConfigurations",
                new Dictionary<string, CommandConfigurationSnapshot>(commandConfigurations, StringComparer.Ordinal));
        }
    }
}

internal sealed class ThrowingDbContextFactory : IDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext() => throw new NotSupportedException();

    public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}

internal sealed class InMemorySqliteDbContextFactory : IDbContextFactory<AppDbContext>, IAsyncDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly DbContextOptions<AppDbContext> _options;

    public InMemorySqliteDbContextFactory()
    {
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var db = new AppDbContext(_options);
        db.Database.EnsureCreated();
    }

    public AppDbContext CreateDbContext() => new(_options);

    public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(CreateDbContext());

    public ValueTask DisposeAsync() => _connection.DisposeAsync();
}

internal sealed class FileBackedSqliteDbContextFactory(string databaseFilePath) : IDbContextFactory<AppDbContext>
{
    private readonly string _connectionString = $"Data Source={databaseFilePath};Pooling=False";
    private readonly DbContextOptions<AppDbContext> _options = new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite($"Data Source={databaseFilePath};Pooling=False")
        .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
        .Options;

    public AppDbContext CreateDbContext() => new(_options);

    public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(CreateDbContext());

    public SqliteConnection CreateConnection() => new(_connectionString);
}

internal sealed class TestAppPathProvider(
    string? legacySettingsFilePath = null,
    string? legacySettingsArchiveFilePath = null,
    string? legacySettingsInvalidFilePath = null,
    string? databaseFilePath = null,
    string? databaseBackupFilePath = null) : IAppPathProvider
{
    private static readonly string SharedRootPath = Path.Combine(Path.GetTempPath(), "yms-tests");

    public string AppDataDirectory => SharedRootPath;
    public string LegacySettingsFilePath => legacySettingsFilePath ?? Path.Combine(SharedRootPath, "AppSettings.json");
    public string LegacySettingsArchiveFilePath => legacySettingsArchiveFilePath ?? Path.Combine(SharedRootPath, "AppSettings.legacy.json");
    public string LegacySettingsInvalidFilePath => legacySettingsInvalidFilePath ?? Path.Combine(SharedRootPath, "AppSettings.invalid.json");
    public string DatabaseFilePath => databaseFilePath ?? Path.Combine(SharedRootPath, "appsettings.db");
    public string DatabaseBackupFilePath => databaseBackupFilePath ?? Path.Combine(SharedRootPath, "appsettings.pre-migration.bak");
    public string InstanceStateFilePath => Path.Combine(SharedRootPath, "instance-state.json");
    public string LogDirectoryPath => Path.Combine(SharedRootPath, "Logs");
    public string CurrentLogFileName => "log-test.log";
    public string UpdateDirectoryPath => Path.Combine(SharedRootPath, "updates");
}

internal sealed class SettingsPersistenceHarness : IAsyncDisposable
{
    private readonly string _tempDirectory;
    private readonly TestAppPathProvider _pathProvider;

    private SettingsPersistenceHarness(
        string tempDirectory,
        TestAppPathProvider pathProvider,
        SettingsService settings,
        RecordingDiagnosticsService diagnostics,
        string databaseFilePath,
        string legacySettingsFilePath,
        string legacySettingsArchiveFilePath,
        string legacySettingsInvalidFilePath,
        string databaseBackupFilePath)
    {
        _tempDirectory = tempDirectory;
        _pathProvider = pathProvider;
        Settings = settings;
        Diagnostics = diagnostics;
        DatabaseFilePath = databaseFilePath;
        LegacySettingsFilePath = legacySettingsFilePath;
        LegacySettingsArchiveFilePath = legacySettingsArchiveFilePath;
        LegacySettingsInvalidFilePath = legacySettingsInvalidFilePath;
        DatabaseBackupFilePath = databaseBackupFilePath;
    }

    public SettingsService Settings { get; }
    public RecordingDiagnosticsService Diagnostics { get; }
    public string DatabaseFilePath { get; }
    public string LegacySettingsFilePath { get; }
    public string LegacySettingsArchiveFilePath { get; }
    public string LegacySettingsInvalidFilePath { get; }
    public string DatabaseBackupFilePath { get; }

    public static SettingsPersistenceHarness Create()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "yms-settings-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        var databaseFilePath = Path.Combine(tempDirectory, "appsettings.db");
        var pathProvider = new TestAppPathProvider(
            legacySettingsFilePath: Path.Combine(tempDirectory, "AppSettings.json"),
            legacySettingsArchiveFilePath: Path.Combine(tempDirectory, "AppSettings.legacy.json"),
            legacySettingsInvalidFilePath: Path.Combine(tempDirectory, "AppSettings.invalid.json"),
            databaseFilePath: databaseFilePath,
            databaseBackupFilePath: Path.Combine(tempDirectory, "appsettings.pre-migration.bak"));

        var diagnostics = new RecordingDiagnosticsService();
        var settings = new SettingsService(
            NullLogger<SettingsService>.Instance,
            new FileBackedSqliteDbContextFactory(databaseFilePath),
            diagnostics,
            pathProvider,
            new AssemblyAppIdentitySource());

        return new SettingsPersistenceHarness(
            tempDirectory,
            pathProvider,
            settings,
            diagnostics,
            pathProvider.DatabaseFilePath,
            pathProvider.LegacySettingsFilePath,
            pathProvider.LegacySettingsArchiveFilePath,
            pathProvider.LegacySettingsInvalidFilePath,
            pathProvider.DatabaseBackupFilePath);
    }

    public SqliteConnection CreateConnection() => new FileBackedSqliteDbContextFactory(DatabaseFilePath).CreateConnection();

    public SettingsService CreateReloadedService() => new(
        NullLogger<SettingsService>.Instance,
        new FileBackedSqliteDbContextFactory(DatabaseFilePath),
        new RecordingDiagnosticsService(),
        _pathProvider,
        new AssemblyAppIdentitySource());

    public async ValueTask DisposeAsync()
    {
        SettingsServiceTestSupport.SetPrivateField(Settings, "_initializationTask", null as Task);

        await Task.Yield();

        if (!Directory.Exists(_tempDirectory))
            return;

        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                Directory.Delete(_tempDirectory, recursive: true);
                return;
            }
            catch (IOException) when (attempt < 4)
            {
                await Task.Delay(50);
            }
            catch (UnauthorizedAccessException) when (attempt < 4)
            {
                await Task.Delay(50);
            }
        }
    }
}

internal sealed class RecordingDiagnosticsService : IAppDiagnosticsService
{
    private readonly List<AppDiagnostic> _recentDiagnostics = [];

    public event EventHandler<AppDiagnostic>? DiagnosticRecorded;

    public IReadOnlyList<AppDiagnostic> RecentDiagnostics => _recentDiagnostics;

    public Task ClearAsync()
    {
        _recentDiagnostics.Clear();
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

        _recentDiagnostics.Add(diagnostic);
        DiagnosticRecorded?.Invoke(this, diagnostic);
        return diagnostic;
    }
}
