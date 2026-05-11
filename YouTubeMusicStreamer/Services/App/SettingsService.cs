using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using YouTubeMusicStreamer.Models;
using YouTubeMusicStreamer.Services.App.Diagnostics;
using YouTubeMusicStreamer.Services.App.LegacySettings;
using YouTubeMusicStreamer.Services.App.Persistence;
using YouTubeMusicStreamer.Utils;

namespace YouTubeMusicStreamer.Services.App;

public class SettingsService
{
    private readonly ILogger<SettingsService> _logger;
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly IAppDiagnosticsService _diagnosticsService;
    private readonly IAppIdentitySource _appIdentitySource;
    private readonly string _legacySettingsFilePath;
    private readonly string _legacySettingsArchiveFilePath;
    private readonly string _legacySettingsInvalidFilePath;
    private readonly string _databaseFilePath;
    private readonly string _databaseBackupFilePath;

    private readonly JsonSerializerOptions _jsonSerializerOptions = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private readonly Lock _sensitiveSettingsLock = new();

    private Task? _initializationTask;
    private bool _isInitialized;

    private AppConfigurationSnapshot _appConfiguration = new(LogLevel.Warning);
    private YouTubeSettingsSnapshot _youTubeSettings = new(null, null, false, 9876, false, string.Empty);
    private TwitchSettingsSnapshot _twitchSettings = new(
        true,
        string.Empty,
        "!",
        null,
        null);
    private QueueSettingsSnapshot _queueSettings = new(false);
    private List<QueueItem> _queueItems = [];
    private List<BlacklistEntry> _blacklistEntries = [];
    private Dictionary<string, CommandConfigurationSnapshot> _commandConfigurations = new(StringComparer.Ordinal);

    private SensitiveSettings? _secureData;
    private Task<SensitiveSettings>? _sensitiveSettingsLoadTask;

    public event Action<AppConfigurationSnapshot> AppConfigurationChanged = delegate { };
    public event Action<YouTubeSettingsSnapshot> YouTubeSettingsChanged = delegate { };
    public event Action<TwitchSettingsSnapshot> TwitchSettingsChanged = delegate { };
    public event Action<QueueSettingsSnapshot> QueueSettingsChanged = delegate { };
    public event Action<IReadOnlyList<QueueItem>> QueueItemsChanged = delegate { };
    public event Action<IReadOnlyList<BlacklistEntry>> BlacklistChanged = delegate { };
    public event Action<IReadOnlyDictionary<string, CommandConfigurationSnapshot>> CommandConfigurationsChanged = delegate { };

    public SettingsService(
        ILogger<SettingsService> logger,
        IDbContextFactory<AppDbContext> dbContextFactory,
        IAppDiagnosticsService diagnosticsService,
        IAppPathProvider appPathProvider,
        IAppIdentitySource appIdentitySource)
    {
        _logger = logger;
        _dbContextFactory = dbContextFactory;
        _diagnosticsService = diagnosticsService;
        _appIdentitySource = appIdentitySource;
        _legacySettingsFilePath = appPathProvider.LegacySettingsFilePath;
        _legacySettingsArchiveFilePath = appPathProvider.LegacySettingsArchiveFilePath;
        _legacySettingsInvalidFilePath = appPathProvider.LegacySettingsInvalidFilePath;
        _databaseFilePath = appPathProvider.DatabaseFilePath;
        _databaseBackupFilePath = appPathProvider.DatabaseBackupFilePath;
        _twitchSettings = _twitchSettings with { ConnectMessage = $"{_appIdentitySource.AppName} connected!" };

        Directory.CreateDirectory(Path.GetDirectoryName(_databaseFilePath)!);
    }

    public Task InitializeAsync() => _initializationTask ??= InitializeCoreAsync();

    public AppConfigurationSnapshot GetAppConfiguration() => _appConfiguration;
    public YouTubeSettingsSnapshot GetYouTubeSettings() => _youTubeSettings;
    public TwitchSettingsSnapshot GetTwitchSettings() => _twitchSettings;
    public QueueSettingsSnapshot GetQueueSettings() => _queueSettings;
    public IReadOnlyList<QueueItem> GetQueueItems() => CloneQueueItems(_queueItems);
    public IReadOnlyList<BlacklistEntry> GetBlacklistEntries() => CloneBlacklistEntries(_blacklistEntries);

    public IReadOnlyDictionary<string, CommandConfigurationSnapshot> GetCommandConfigurations() =>
        CloneCommandConfigurations(_commandConfigurations);

    public CommandConfigurationSnapshot? GetCommandConfiguration(string commandKey) =>
        _commandConfigurations.TryGetValue(commandKey, out var configuration)
            ? configuration.Clone()
            : null;

    public async Task SaveAppConfigurationAsync(AppConfigurationSnapshot snapshot)
    {
        await EnsureInitializedAsync();

        await UpdateAsync(async db =>
        {
            var entity = await db.AppConfiguration.AsTracking().SingleAsync(x => x.Id == 1);
            entity.LogLevel = snapshot.LogLevel;
        });

        _appConfiguration = snapshot;
        LoggingConfig.SetRuntimeLogLevel(snapshot.LogLevel);
        AppConfigurationChanged(_appConfiguration);
    }

    public async Task SaveYouTubeSettingsAsync(YouTubeSettingsSnapshot snapshot)
    {
        await EnsureInitializedAsync();
        await UpdateAsync(async db =>
        {
            var entity = await db.YouTubeSettings.AsTracking().SingleAsync(x => x.Id == 1);
            entity.Host = snapshot.Host;
            entity.Port = snapshot.Port;
            entity.AutoStartServer = snapshot.AutoStartServer;
            entity.PublicPort = snapshot.PublicPort;
            entity.AllowAudioCapture = snapshot.AllowAudioCapture;
            entity.AudioCaptureDevice = snapshot.AudioCaptureDevice;
        });

        _youTubeSettings = snapshot;
        YouTubeSettingsChanged(_youTubeSettings);
    }

    public async Task SaveTwitchSettingsAsync(TwitchSettingsSnapshot snapshot)
    {
        await EnsureInitializedAsync();
        await UpdateAsync(async db =>
        {
            var entity = await db.TwitchSettings.AsTracking().SingleAsync(x => x.Id == 1);
            entity.SendMessageOnConnect = snapshot.SendMessageOnConnect;
            entity.ConnectMessage = snapshot.ConnectMessage;
            entity.CommandPrefix = snapshot.CommandPrefix;
            entity.BroadcasterAccountId = snapshot.BroadcasterAccount?.AccountId;
            entity.BroadcasterLogin = snapshot.BroadcasterAccount?.Login;
            entity.BroadcasterDisplayName = snapshot.BroadcasterAccount?.DisplayName;
            entity.BroadcasterProfileImageUrl = snapshot.BroadcasterAccount?.ProfileImageUrl;
            entity.BotAccountId = snapshot.BotAccount?.AccountId;
            entity.BotLogin = snapshot.BotAccount?.Login;
            entity.BotDisplayName = snapshot.BotAccount?.DisplayName;
            entity.BotProfileImageUrl = snapshot.BotAccount?.ProfileImageUrl;
        });

        _twitchSettings = snapshot;
        TwitchSettingsChanged(_twitchSettings);
    }

    public async Task SaveQueueSettingsAsync(QueueSettingsSnapshot snapshot)
    {
        await EnsureInitializedAsync();
        await UpdateAsync(async db =>
        {
            var entity = await db.QueueSettings.AsTracking().SingleAsync(x => x.Id == 1);
            entity.QueueActive = snapshot.QueueActive;
        });

        _queueSettings = snapshot;
        QueueSettingsChanged(_queueSettings);
    }

    public async Task UpdateQueueItemsAsync(Action<List<QueueItem>> update)
    {
        await EnsureInitializedAsync();
        var updatedItems = CloneQueueItems(_queueItems);
        update(updatedItems);
        await ReplaceQueueItemsAsync(updatedItems);
    }

    public async Task ReplaceQueueItemsAsync(IReadOnlyList<QueueItem> items)
    {
        await EnsureInitializedAsync();
        var clonedItems = CloneQueueItems(items);

        await UpdateAsync(async db =>
        {
            db.QueueItems.RemoveRange(await db.QueueItems.AsTracking().ToListAsync());

            var entities = clonedItems.Select((item, index) => new QueueItemEntity
            {
                SortOrder = index,
                VideoId = item.Id,
                Requester = item.Requester,
                Message = item.Message,
                RequestedAtUtc = item.RequestedAt,
                EmbedJson = item.Embed is null ? null : JsonSerializer.Serialize(item.Embed, _jsonSerializerOptions)
            });

            await db.QueueItems.AddRangeAsync(entities);
        });

        _queueItems = clonedItems;
        QueueItemsChanged(CloneQueueItems(_queueItems));
    }

    public async Task UpdateBlacklistEntriesAsync(Action<List<BlacklistEntry>> update)
    {
        await EnsureInitializedAsync();
        var entries = CloneBlacklistEntries(_blacklistEntries);
        update(entries);
        await ReplaceBlacklistEntriesAsync(entries);
    }

    public async Task ReplaceBlacklistEntriesAsync(IReadOnlyList<BlacklistEntry> entries)
    {
        await EnsureInitializedAsync();
        var clonedEntries = CloneBlacklistEntries(entries);

        await UpdateAsync(async db =>
        {
            db.BlacklistEntries.RemoveRange(await db.BlacklistEntries.AsTracking().ToListAsync());
            await db.BlacklistEntries.AddRangeAsync(clonedEntries.Select(entry => new BlacklistEntryEntity
            {
                Url = entry.Url.ToString(),
                Description = entry.Description
            }));
        });

        _blacklistEntries = clonedEntries;
        BlacklistChanged(CloneBlacklistEntries(_blacklistEntries));
    }

    public async Task SaveCommandConfigurationAsync(string commandKey, CommandConfigurationSnapshot configuration)
    {
        await EnsureInitializedAsync();
        var clone = configuration.Clone();
        clone.ChatTriggerMode = NormalizeChatTriggerMode(clone.ChatTriggerMode, clone.BitsThreshold);

        await UpdateAsync(async db =>
        {
            var entity = await db.CommandConfigurations.AsTracking().SingleOrDefaultAsync(x => x.CommandKey == commandKey);
            if (entity is null)
            {
                entity = new CommandConfigurationEntity { CommandKey = commandKey };
                await db.CommandConfigurations.AddAsync(entity);
            }

            entity.Trigger = clone.Trigger;
            entity.ChatTriggerMode = clone.ChatTriggerMode;
            entity.RewardTriggerMode = clone.RewardTriggerMode;
            entity.RequiredAccessLevel = clone.RequiredAccessLevel;
            entity.CooldownScope = clone.CooldownScope;
            entity.BitsThreshold = clone.BitsThreshold;
            entity.RewardId = clone.RewardBinding?.RewardId;
            entity.RewardBroadcasterAccountId = clone.RewardBinding?.BroadcasterAccountId;
            entity.ManagedRewardName = clone.RewardBinding?.ManagedRewardName;
            entity.ManagedRewardPrompt = clone.RewardBinding?.ManagedRewardPrompt;
            entity.ManagedRewardCost = clone.RewardBinding?.ManagedRewardCost ?? 0;
            entity.ManagedRewardRequiresUserInput = clone.RewardBinding?.ManagedRewardRequiresUserInput ?? false;
            entity.ManagedRewardCompatibilityState = clone.RewardBinding?.ManagedRewardCompatibilityState ?? ManagedRewardCompatibilityState.Unknown;
            entity.ManagedRewardCompatibilityMessage = clone.RewardBinding?.ManagedRewardCompatibilityMessage;
            entity.Cooldown = clone.Cooldown;
            entity.Response = clone.Response;
            entity.AccessDeniedResponse = clone.AccessDeniedResponse;
        });

        _commandConfigurations[commandKey] = clone;
        CommandConfigurationsChanged(CloneCommandConfigurations(_commandConfigurations));
    }

    public SensitiveSettings GetSensitiveSettings() => EnsureSensitiveSettingsLoadedAsync().ConfigureAwait(false).GetAwaiter().GetResult();

    public async Task<SensitiveSettings> GetSensitiveSettingsAsync() => await EnsureSensitiveSettingsLoadedAsync();

    public Task PreloadSensitiveSettingsAsync() => EnsureSensitiveSettingsLoadedAsync();

    public async Task SaveSensitiveSettingAsync(Action<SensitiveSettings> update)
    {
        var secureData = await EnsureSensitiveSettingsLoadedAsync();
        update(secureData);
        await SaveSensitiveSettingsToFileAsync();
    }

    private async Task InitializeCoreAsync()
    {
        await _initializationLock.WaitAsync();
        try
        {
            if (_isInitialized)
                return;

            await using var db = await _dbContextFactory.CreateDbContextAsync();
            await PrepareDatabaseAsync(db);

            if (!await HasStructuredDataAsync(db))
            {
                if (File.Exists(_legacySettingsFilePath))
                {
                    if (await TryImportLegacyJsonAsync(db))
                        ArchiveLegacySettingsFile();
                    else
                        await SeedDefaultStateAsync(db);
                }
                else
                {
                    await SeedDefaultStateAsync(db);
                }
            }

            await LoadCachedStateAsync(db);
            _isInitialized = true;
        }
        catch (Exception ex)
        {
            _logger.Diagnostic(_diagnosticsService, AppDiagnosticSubsystem.Settings)
                .Error(AppDiagnosticCategory.Persistence, "Failed to initialize settings persistence")
                .WithDetail(ex.Message)
                .WithException(ex)
                .Toast()
                .Write();
            throw;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private async Task<bool> HasStructuredDataAsync(AppDbContext db) =>
        await db.AppConfiguration.AnyAsync() ||
        await db.YouTubeSettings.AnyAsync() ||
        await db.TwitchSettings.AnyAsync() ||
        await db.QueueSettings.AnyAsync() ||
        await db.CommandConfigurations.AnyAsync() ||
        await db.QueueItems.AnyAsync() ||
        await db.BlacklistEntries.AnyAsync();

    private async Task PrepareDatabaseAsync(AppDbContext db)
    {
        var databaseExists = File.Exists(_databaseFilePath);

        if (databaseExists)
        {
            var pendingMigrations = await db.Database.GetPendingMigrationsAsync();
            if (pendingMigrations.Any())
                BackupExistingDatabase();
        }

        await db.Database.MigrateAsync();
    }

    private void BackupExistingDatabase()
    {
        try
        {
            if (!File.Exists(_databaseFilePath))
                return;

            File.Copy(_databaseFilePath, _databaseBackupFilePath, overwrite: true);
            _logger.LogInformation("Created database backup before migration at {BackupPath}", _databaseBackupFilePath);
        }
        catch (Exception ex)
        {
            _logger.Diagnostic(_diagnosticsService, AppDiagnosticSubsystem.Settings)
                .Error(AppDiagnosticCategory.Persistence, "Failed to create database backup before migration")
                .WithDetail(ex.Message)
                .WithException(ex)
                .Write();
            throw;
        }
    }

    private async Task SeedDefaultStateAsync(AppDbContext db)
    {
        _logger.LogInformation("Creating default SQLite settings state");

        await db.AppConfiguration.AddAsync(new AppConfigurationEntity
        {
            Id = 1,
            LogLevel = _appConfiguration.LogLevel
        });

        await db.YouTubeSettings.AddAsync(new YouTubeSettingsEntity
        {
            Id = 1,
            Host = _youTubeSettings.Host,
            Port = _youTubeSettings.Port,
            AutoStartServer = _youTubeSettings.AutoStartServer,
            PublicPort = _youTubeSettings.PublicPort,
            AllowAudioCapture = _youTubeSettings.AllowAudioCapture,
            AudioCaptureDevice = _youTubeSettings.AudioCaptureDevice
        });

        await db.TwitchSettings.AddAsync(new TwitchSettingsEntity
        {
            Id = 1,
            SendMessageOnConnect = _twitchSettings.SendMessageOnConnect,
            ConnectMessage = _twitchSettings.ConnectMessage,
            CommandPrefix = _twitchSettings.CommandPrefix
        });

        await db.QueueSettings.AddAsync(new QueueSettingsEntity
        {
            Id = 1,
            QueueActive = _queueSettings.QueueActive
        });

        await db.SaveChangesAsync();
    }

    private async Task ImportLegacyJsonAsync(AppDbContext db)
    {
        var legacy = LoadLegacyAppSettings();
        _logger.LogInformation("Migrating legacy AppSettings.json into SQLite");

        await db.AppConfiguration.AddAsync(new AppConfigurationEntity
        {
            Id = 1,
            LogLevel = legacy.LogLevel
        });

        await db.YouTubeSettings.AddAsync(new YouTubeSettingsEntity
        {
            Id = 1,
            Host = legacy.YouTubeHost,
            Port = legacy.YouTubePort,
            AutoStartServer = legacy.AutoStartServer,
            PublicPort = legacy.PublicPort,
            AllowAudioCapture = legacy.AllowAudioCapture,
            AudioCaptureDevice = legacy.AudioCaptureDevice
        });

        await db.TwitchSettings.AddAsync(new TwitchSettingsEntity
        {
            Id = 1,
            SendMessageOnConnect = legacy.TwitchSendMessageOnConnect,
            ConnectMessage = legacy.TwitchConnectMessage,
            CommandPrefix = NormalizeCommandPrefix(legacy.TwitchCommandPrefix)
        });

        await db.QueueSettings.AddAsync(new QueueSettingsEntity
        {
            Id = 1,
            QueueActive = legacy.QueueActive
        });

        await db.QueueItems.AddRangeAsync(legacy.Queue.Select((item, index) => new QueueItemEntity
        {
            SortOrder = index,
            VideoId = item.Id,
            Requester = item.Requester,
            Message = item.Message,
            RequestedAtUtc = item.RequestedAt,
            EmbedJson = item.Embed is null ? null : JsonSerializer.Serialize(item.Embed, _jsonSerializerOptions)
        }));

        await db.BlacklistEntries.AddRangeAsync(legacy.Blacklist.Select(entry => new BlacklistEntryEntity
        {
            Url = entry.Url.ToString(),
            Description = entry.Description
        }));

        await db.CommandConfigurations.AddRangeAsync(
            legacy.Commands.Select(command => MapLegacyCommandConfiguration(command.Key, command.Value)));

        await db.SaveChangesAsync();
    }

    private async Task<bool> TryImportLegacyJsonAsync(AppDbContext db)
    {
        try
        {
            await ImportLegacyJsonAsync(db);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Legacy AppSettings.json migration failed. Falling back to default seeded state.");
            ArchiveInvalidLegacySettingsFile();
            return false;
        }
    }

    private static CommandConfigurationEntity MapLegacyCommandConfiguration(string commandKey, LegacyCommandSettings legacy)
    {
        var chatTriggerMode = ChatTriggerMode.Disabled;
        var rewardTriggerMode = RewardTriggerMode.Disabled;
        var bitsThreshold = 0u;
        CommandRewardBindingSnapshot? rewardBinding = null;

        if (!legacy.IsEnabled)
        {
            chatTriggerMode = ChatTriggerMode.Disabled;
            rewardTriggerMode = RewardTriggerMode.Disabled;
        }
        else if (!string.IsNullOrWhiteSpace(legacy.RewardId))
        {
            rewardTriggerMode = RewardTriggerMode.Existing;
            rewardBinding = new CommandRewardBindingSnapshot { RewardId = legacy.RewardId };
            chatTriggerMode = legacy.RequiredBits > 0 ? ChatTriggerMode.Disabled : ChatTriggerMode.Chat;
        }
        else if (legacy.RequiredBits > 0)
        {
            chatTriggerMode = ChatTriggerMode.BitsOnly;
            bitsThreshold = legacy.RequiredBits;
        }
        else
        {
            chatTriggerMode = ChatTriggerMode.Chat;
        }

        return new CommandConfigurationEntity
        {
            CommandKey = commandKey,
            Trigger = legacy.Trigger,
            ChatTriggerMode = chatTriggerMode,
            RewardTriggerMode = rewardTriggerMode,
            RequiredAccessLevel = CommandAccessLevel.Everyone,
            CooldownScope = CommandCooldownScope.Global,
            BitsThreshold = bitsThreshold,
            RewardId = rewardBinding?.RewardId,
            RewardBroadcasterAccountId = rewardBinding?.BroadcasterAccountId,
            ManagedRewardName = rewardBinding?.ManagedRewardName,
            Cooldown = legacy.Cooldown,
            Response = legacy.Response,
            AccessDeniedResponse = null
        };
    }

    private LegacyAppSettings LoadLegacyAppSettings()
    {
        try
        {
            if (!File.Exists(_legacySettingsFilePath))
                return new LegacyAppSettings();

            var json = File.ReadAllText(_legacySettingsFilePath);
            return JsonSerializer.Deserialize<LegacyAppSettings>(json) ?? new LegacyAppSettings();
        }
        catch (Exception ex)
        {
            _logger.Diagnostic(_diagnosticsService, AppDiagnosticSubsystem.Settings)
                .Error(AppDiagnosticCategory.Configuration, "Failed to load legacy AppSettings.json for migration")
                .WithDetail(ex.Message)
                .WithException(ex)
                .Write();
            throw;
        }
    }

    private void ArchiveLegacySettingsFile()
    {
        try
        {
            if (File.Exists(_legacySettingsArchiveFilePath))
                File.Delete(_legacySettingsArchiveFilePath);

            File.Move(_legacySettingsFilePath, _legacySettingsArchiveFilePath);
        }
        catch (Exception ex)
        {
            _logger.Diagnostic(_diagnosticsService, AppDiagnosticSubsystem.Settings)
                .Warning(AppDiagnosticCategory.Persistence, "Failed to archive legacy AppSettings.json after migration")
                .WithLogLevel(LogLevel.Error)
                .WithDetail(ex.Message)
                .WithException(ex)
                .Write();
            throw;
        }
    }

    private void ArchiveInvalidLegacySettingsFile()
    {
        try
        {
            if (!File.Exists(_legacySettingsFilePath))
                return;

            if (File.Exists(_legacySettingsInvalidFilePath))
                File.Delete(_legacySettingsInvalidFilePath);

            File.Move(_legacySettingsFilePath, _legacySettingsInvalidFilePath);
        }
        catch (Exception ex)
        {
            _logger.Diagnostic(_diagnosticsService, AppDiagnosticSubsystem.Settings)
                .Warning(AppDiagnosticCategory.Persistence, "Failed to archive invalid legacy AppSettings.json")
                .WithLogLevel(LogLevel.Error)
                .WithDetail(ex.Message)
                .WithException(ex)
                .Write();
            throw;
        }
    }

    private async Task LoadCachedStateAsync(AppDbContext db)
    {
        var appConfiguration = await db.AppConfiguration.SingleAsync(x => x.Id == 1);
        _appConfiguration = new AppConfigurationSnapshot(appConfiguration.LogLevel);
        LoggingConfig.SetRuntimeLogLevel(_appConfiguration.LogLevel);

        var youTubeSettings = await db.YouTubeSettings.SingleAsync(x => x.Id == 1);
        _youTubeSettings = new YouTubeSettingsSnapshot(
            youTubeSettings.Host,
            youTubeSettings.Port,
            youTubeSettings.AutoStartServer,
            youTubeSettings.PublicPort,
            youTubeSettings.AllowAudioCapture,
            youTubeSettings.AudioCaptureDevice);

        var twitchSettings = await db.TwitchSettings.SingleAsync(x => x.Id == 1);
        _twitchSettings = new TwitchSettingsSnapshot(
            twitchSettings.SendMessageOnConnect,
            twitchSettings.ConnectMessage,
            NormalizeCommandPrefix(twitchSettings.CommandPrefix),
            new TwitchAccountMetadataSnapshot(
                twitchSettings.BroadcasterAccountId,
                twitchSettings.BroadcasterLogin,
                twitchSettings.BroadcasterDisplayName,
                twitchSettings.BroadcasterProfileImageUrl),
            new TwitchAccountMetadataSnapshot(
                twitchSettings.BotAccountId,
                twitchSettings.BotLogin,
                twitchSettings.BotDisplayName,
                twitchSettings.BotProfileImageUrl));

        var queueSettings = await db.QueueSettings.SingleAsync(x => x.Id == 1);
        _queueSettings = new QueueSettingsSnapshot(queueSettings.QueueActive);

        var queueItemEntities = await db.QueueItems
            .OrderBy(x => x.SortOrder)
            .ToListAsync();
        _queueItems = queueItemEntities.Select(MapQueueItemEntity).ToList();

        _blacklistEntries = await db.BlacklistEntries
            .Select(x => new BlacklistEntry(new Uri(x.Url), x.Description))
            .ToListAsync();

        _commandConfigurations = await db.CommandConfigurations
            .ToDictionaryAsync(
                x => x.CommandKey,
                MapCommandConfigurationEntity,
                StringComparer.Ordinal);
    }

    private static CommandConfigurationSnapshot MapCommandConfigurationEntity(CommandConfigurationEntity entity) => new()
    {
        Trigger = entity.Trigger,
        ChatTriggerMode = NormalizeChatTriggerMode(entity.ChatTriggerMode, entity.BitsThreshold),
        RewardTriggerMode = entity.RewardTriggerMode,
        RequiredAccessLevel = entity.RequiredAccessLevel,
        CooldownScope = entity.CooldownScope,
        BitsThreshold = entity.BitsThreshold,
        RewardBinding = string.IsNullOrWhiteSpace(entity.RewardId) &&
                        string.IsNullOrWhiteSpace(entity.RewardBroadcasterAccountId) &&
                        string.IsNullOrWhiteSpace(entity.ManagedRewardName) &&
                        string.IsNullOrWhiteSpace(entity.ManagedRewardPrompt) &&
                        entity.ManagedRewardCost == 0 &&
                        !entity.ManagedRewardRequiresUserInput &&
                        entity.ManagedRewardCompatibilityState == ManagedRewardCompatibilityState.Unknown &&
                        string.IsNullOrWhiteSpace(entity.ManagedRewardCompatibilityMessage)
            ? null
            : new CommandRewardBindingSnapshot
            {
                RewardId = entity.RewardId,
                BroadcasterAccountId = entity.RewardBroadcasterAccountId,
                ManagedRewardName = entity.ManagedRewardName,
                ManagedRewardPrompt = entity.ManagedRewardPrompt,
                ManagedRewardCost = entity.ManagedRewardCost,
                ManagedRewardRequiresUserInput = entity.ManagedRewardRequiresUserInput,
                ManagedRewardCompatibilityState = entity.ManagedRewardCompatibilityState,
                ManagedRewardCompatibilityMessage = entity.ManagedRewardCompatibilityMessage
            },
        Cooldown = entity.Cooldown,
        Response = entity.Response,
        AccessDeniedResponse = entity.AccessDeniedResponse
    };

    private static ChatTriggerMode NormalizeChatTriggerMode(ChatTriggerMode mode, uint bitsThreshold) =>
        mode == ChatTriggerMode.ChatAndBits
            ? bitsThreshold > 0 ? ChatTriggerMode.BitsOnly : ChatTriggerMode.Chat
            : mode;

    private static string NormalizeCommandPrefix(string? commandPrefix) =>
        string.IsNullOrWhiteSpace(commandPrefix) ? "!" : commandPrefix;

    private QueueItem MapQueueItemEntity(QueueItemEntity entity)
    {
        var embed = string.IsNullOrWhiteSpace(entity.EmbedJson)
            ? null
            : JsonSerializer.Deserialize<YouTubeEmbed>(entity.EmbedJson, _jsonSerializerOptions);

        return new QueueItem(entity.VideoId, entity.Requester, entity.Message, embed)
        {
            RequestedAt = entity.RequestedAtUtc
        };
    }

    private async Task UpdateAsync(Func<AppDbContext, Task> update)
    {
        await _semaphore.WaitAsync();
        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            await update(db);
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.Diagnostic(_diagnosticsService, AppDiagnosticSubsystem.Settings)
                .Error(AppDiagnosticCategory.Persistence, "Failed to persist settings update")
                .WithDetail(ex.Message)
                .WithException(ex)
                .Toast()
                .Write();
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task EnsureInitializedAsync()
    {
        if (_isInitialized)
            return;

        await InitializeAsync();
    }

    private async Task SaveSensitiveSettingsToFileAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            var sensitiveSettings = await EnsureSensitiveSettingsLoadedAsync();
            foreach (var property in typeof(SensitiveSettings).GetProperties())
            {
                var value = property.GetValue(sensitiveSettings);
                if (value is null)
                {
                    SecureStorage.Default.Remove(property.Name);
                    continue;
                }

                var json = JsonSerializer.Serialize(value, _jsonSerializerOptions);
                await SecureStorage.Default.SetAsync(property.Name, json);
            }
        }
        catch (Exception ex)
        {
            _logger.Diagnostic(_diagnosticsService, AppDiagnosticSubsystem.Settings)
                .Warning(AppDiagnosticCategory.Persistence, "Failed to save sensitive data")
                .WithLogLevel(LogLevel.Error)
                .WithDetail(ex.Message)
                .WithException(ex)
                .Write();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<SensitiveSettings> LoadSensitiveSettingsAsync()
    {
        try
        {
            var sensitiveSettings = new SensitiveSettings();
            foreach (var property in typeof(SensitiveSettings).GetProperties())
            {
                var json = await SecureStorage.Default.GetAsync(property.Name);
                if (json is null) continue;

                var value = JsonSerializer.Deserialize(json, property.PropertyType, _jsonSerializerOptions);
                property.SetValue(sensitiveSettings, value);
            }

            return sensitiveSettings;
        }
        catch (Exception ex)
        {
            _logger.Diagnostic(_diagnosticsService, AppDiagnosticSubsystem.Settings)
                .Warning(AppDiagnosticCategory.Persistence, "Failed to load sensitive data")
                .WithLogLevel(LogLevel.Error)
                .WithDetail(ex.Message)
                .WithException(ex)
                .Write();
        }

        return new SensitiveSettings();
    }

    private Task<SensitiveSettings> EnsureSensitiveSettingsLoadedAsync()
    {
        lock (_sensitiveSettingsLock)
        {
            if (_secureData is not null)
                return Task.FromResult(_secureData);

            _sensitiveSettingsLoadTask ??= LoadSensitiveSettingsAsync();
            return AwaitSensitiveSettingsLoadAsync(_sensitiveSettingsLoadTask);
        }
    }

    private async Task<SensitiveSettings> AwaitSensitiveSettingsLoadAsync(Task<SensitiveSettings> loadTask)
    {
        try
        {
            var loaded = await loadTask;

            lock (_sensitiveSettingsLock)
            {
                _secureData ??= loaded;
                return _secureData;
            }
        }
        finally
        {
            lock (_sensitiveSettingsLock)
            {
                if (ReferenceEquals(_sensitiveSettingsLoadTask, loadTask))
                    _sensitiveSettingsLoadTask = null;
            }
        }
    }

    private static List<QueueItem> CloneQueueItems(IEnumerable<QueueItem> items) =>
        items.Select(item => new QueueItem(item.Id, item.Requester, item.Message, item.Embed)
        {
            RequestedAt = item.RequestedAt
        }).ToList();

    private static List<BlacklistEntry> CloneBlacklistEntries(IEnumerable<BlacklistEntry> entries) =>
        entries.Select(entry => new BlacklistEntry(entry.Url, entry.Description)).ToList();

    private static IReadOnlyDictionary<string, CommandConfigurationSnapshot> CloneCommandConfigurations(
        IReadOnlyDictionary<string, CommandConfigurationSnapshot> configurations) =>
        configurations.ToDictionary(x => x.Key, x => x.Value.Clone(), StringComparer.Ordinal);

}
