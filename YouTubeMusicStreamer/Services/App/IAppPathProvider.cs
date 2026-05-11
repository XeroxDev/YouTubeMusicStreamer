using System.Reflection;

namespace YouTubeMusicStreamer.Services.App;

public interface IAppPathProvider
{
    string AppDataDirectory { get; }
    string LegacySettingsFilePath { get; }
    string LegacySettingsArchiveFilePath { get; }
    string LegacySettingsInvalidFilePath { get; }
    string DatabaseFilePath { get; }
    string DatabaseBackupFilePath { get; }
    string InstanceStateFilePath { get; }
    string LogDirectoryPath { get; }
    string CurrentLogFileName { get; }
    string UpdateDirectoryPath { get; }
}

public sealed class AppPathProvider : IAppPathProvider
{
    private static string AppName => Assembly.GetExecutingAssembly().GetName().Name ?? "YouTubeMusicStreamer";

    public string AppDataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppName);
    public string LegacySettingsFilePath => Path.Combine(AppDataDirectory, "AppSettings.json");
    public string LegacySettingsArchiveFilePath => Path.Combine(AppDataDirectory, "AppSettings.legacy.json");
    public string LegacySettingsInvalidFilePath => Path.Combine(AppDataDirectory, "AppSettings.invalid.json");
    public string DatabaseFilePath => Path.Combine(AppDataDirectory, "appsettings.db");
    public string DatabaseBackupFilePath => Path.Combine(AppDataDirectory, "appsettings.pre-migration.bak");
    public string InstanceStateFilePath => Path.Combine(AppDataDirectory, "instance-state.json");
    public string LogDirectoryPath => Path.Combine(AppDataDirectory, "Logs");
    public string CurrentLogFileName => $"log-{DateTime.Now:yyyyMMdd}.log";
    public string UpdateDirectoryPath => Path.Combine(AppDataDirectory, "updates");
}
