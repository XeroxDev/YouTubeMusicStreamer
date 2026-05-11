using NuGet.Versioning;

namespace YouTubeMusicStreamer.Services.App;

public interface IAppLaunchState
{
    SemanticVersion? ConsumeFirstInstallVersion();
    SemanticVersion? ConsumeUpdatedVersion();
}

public sealed class AppLaunchState(SemanticVersion? firstInstallVersion, SemanticVersion? updatedVersion) : IAppLaunchState
{
    private readonly Lock _lock = new();
    private SemanticVersion? _firstInstallVersion = firstInstallVersion;
    private SemanticVersion? _updatedVersion = updatedVersion;

    public SemanticVersion? ConsumeFirstInstallVersion()
    {
        lock (_lock)
        {
            var value = _firstInstallVersion;
            _firstInstallVersion = null;
            return value;
        }
    }

    public SemanticVersion? ConsumeUpdatedVersion()
    {
        lock (_lock)
        {
            var value = _updatedVersion;
            _updatedVersion = null;
            return value;
        }
    }
}

public static class AppLaunchStateCapture
{
    private static readonly Lock SyncRoot = new();
    private static SemanticVersion? _firstInstallVersion;
    private static SemanticVersion? _updatedVersion;

    public static void RecordFirstInstall(SemanticVersion version)
    {
        lock (SyncRoot)
        {
            _firstInstallVersion = version;
        }
    }

    public static void RecordUpdated(SemanticVersion version)
    {
        lock (SyncRoot)
        {
            _updatedVersion = version;
        }
    }

    public static AppLaunchState CreateState()
    {
        lock (SyncRoot)
        {
            return new AppLaunchState(_firstInstallVersion, _updatedVersion);
        }
    }
}
