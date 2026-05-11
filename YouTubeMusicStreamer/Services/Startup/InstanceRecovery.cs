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

using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Windows.AppLifecycle;
using YouTubeMusicStreamer.Services.App;

namespace YouTubeMusicStreamer.Services.Startup;

internal static class InstanceRecovery
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan ReadyStateStaleAfter = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan TransitionalStateStaleAfter = TimeSpan.FromSeconds(15);
    private static readonly AssemblyAppIdentitySource AppIdentitySource = new();
    private static readonly AppPathProvider AppPathProvider = new();

    public static async Task<bool> RedirectOrRecoverAsync(ILogger logger, AppInstance instance)
    {
        return await RedirectOrRecoverAsync(logger, new WindowsInstanceRecoveryEnvironment(instance));
    }

    internal static async Task<bool> RedirectOrRecoverAsync(ILogger logger, IInstanceRecoveryEnvironment environment)
    {
        var pipeResult = await environment.TryShowWindowAsync();
        if (pipeResult?.Success == true)
        {
            logger.LogInformation("Primary instance responded to ShowWindow");
            return true;
        }

        var health = environment.TryReadStateFile(logger);
        if (health is null)
        {
            logger.LogWarning("Primary instance pipe did not respond and no valid state file was available; falling back to activation redirect");
            await environment.RedirectActivationAsync(logger);
            return true;
        }

        if (!string.Equals(health.AppVersion, environment.CurrentAppVersion, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("State file app version did not match current app version; refusing forced recovery");
            await environment.RedirectActivationAsync(logger);
            return true;
        }

        if (!environment.TryGetVerifiedProcess(health.ProcessId, out var process))
        {
            logger.LogWarning("State file referenced missing or mismatched process; retrying instance acquisition");
            return await TryAcquireAfterStaleOwnerAsync(logger, environment);
        }

        using (process)
        {
            if (!IsStale(health))
            {
                logger.LogWarning("Primary instance process still looks alive; falling back to activation redirect");
                await environment.RedirectActivationAsync(logger);
                return true;
            }

            logger.LogWarning("Verified stale primary instance detected; attempting conservative recovery");

            try
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to terminate stale verified primary instance");
                await environment.RedirectActivationAsync(logger);
                return true;
            }
        }

        return await TryAcquireAfterStaleOwnerAsync(logger, environment);
    }

    private static async Task<bool> TryAcquireAfterStaleOwnerAsync(ILogger logger, IInstanceRecoveryEnvironment environment)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            await environment.DelayAsync(TimeSpan.FromMilliseconds(250));
            if (environment.TryAcquirePrimaryOwnership())
            {
                logger.LogInformation("Recovered primary-instance ownership after stale-instance validation");
                return false;
            }
        }

        logger.LogWarning("Could not reacquire instance ownership after recovery attempt; falling back to exit");
        return true;
    }

    private static async Task RedirectActivationAsync(ILogger logger, AppInstance instance)
    {
        logger.LogInformation("Redirecting activation to current instance");
        var first = AppInstance.GetCurrent();
        await instance.RedirectActivationToAsync(first.GetActivatedEventArgs());
    }

    private static InstanceHealthState? TryReadStateFile(ILogger logger)
    {
        try
        {
            var path = AppPathProvider.InstanceStateFilePath;
            if (!File.Exists(path))
                return null;

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<InstanceHealthState>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read instance state file");
            return null;
        }
    }

    private static bool TryGetVerifiedProcess(int pid, out Process process)
    {
        try
        {
            process = Process.GetProcessById(pid);
            if (process.HasExited)
                return false;

            return string.Equals(process.ProcessName, AppIdentitySource.AppName, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            process = null!;
            return false;
        }
    }

    private static bool IsStale(InstanceHealthState health)
    {
        var age = DateTime.UtcNow - health.LastUpdatedUtc;
        return health.State switch
        {
            InstanceRuntimeState.Ready => age > ReadyStateStaleAfter,
            InstanceRuntimeState.Starting => age > TransitionalStateStaleAfter,
            InstanceRuntimeState.ShuttingDown => age > TransitionalStateStaleAfter,
            InstanceRuntimeState.Unhealthy => age > TransitionalStateStaleAfter,
            _ => true
        };
    }

    private sealed class WindowsInstanceRecoveryEnvironment(AppInstance instance) : IInstanceRecoveryEnvironment
    {
        public string CurrentAppVersion => AppInfo.Current.VersionString;

        public Task<InstanceCommandResult?> TryShowWindowAsync() => InstanceControlClient.TryShowWindowAsync();

        public InstanceHealthState? TryReadStateFile(ILogger logger) => InstanceRecovery.TryReadStateFile(logger);

        public bool TryGetVerifiedProcess(int pid, out IInstanceRecoveryProcess process)
        {
            if (InstanceRecovery.TryGetVerifiedProcess(pid, out var realProcess))
            {
                process = new WindowsInstanceRecoveryProcess(realProcess);
                return true;
            }

            process = null!;
            return false;
        }

        public async Task RedirectActivationAsync(ILogger logger) => await InstanceRecovery.RedirectActivationAsync(logger, instance);

        public async Task DelayAsync(TimeSpan delay) => await Task.Delay(delay);

        public bool TryAcquirePrimaryOwnership() => AppInstance.FindOrRegisterForKey(AppIdentitySource.AppName).IsCurrent;
    }

    private sealed class WindowsInstanceRecoveryProcess(Process process) : IInstanceRecoveryProcess
    {
        public void Kill(bool entireProcessTree) => process.Kill(entireProcessTree);

        public void WaitForExit(int milliseconds) => process.WaitForExit(milliseconds);

        public void Dispose() => process.Dispose();
    }
}

internal interface IInstanceRecoveryEnvironment
{
    string CurrentAppVersion { get; }
    Task<InstanceCommandResult?> TryShowWindowAsync();
    InstanceHealthState? TryReadStateFile(ILogger logger);
    bool TryGetVerifiedProcess(int pid, out IInstanceRecoveryProcess process);
    Task RedirectActivationAsync(ILogger logger);
    Task DelayAsync(TimeSpan delay);
    bool TryAcquirePrimaryOwnership();
}

internal interface IInstanceRecoveryProcess : IDisposable
{
    void Kill(bool entireProcessTree);
    void WaitForExit(int milliseconds);
}
