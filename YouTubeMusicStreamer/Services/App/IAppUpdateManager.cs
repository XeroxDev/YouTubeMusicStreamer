// This file is part of YouTubeMusicStreamer.
// Copyright (C) 2026 Dominic Ris
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
namespace YouTubeMusicStreamer.Services.App;

public interface IAppUpdateManager
{
    bool IsInstalled { get; }
    bool IsPortable { get; }
    SemanticVersion? CurrentVersion { get; }
    VelopackAsset? UpdatePendingRestart { get; }
    Task<UpdateInfo?> CheckForUpdatesAsync();
    Task DownloadUpdatesAsync(UpdateInfo updateInfo);
    void ApplyUpdatesAndRestart(UpdateInfo updateInfo);
}

public interface IAppUpdateManagerFactory
{
    IAppUpdateManager Create(SemanticVersion internalVersion);
}

#pragma warning disable CS9113
public sealed class VelopackUpdateManagerFactory(IAppPathProvider appPathProvider) : IAppUpdateManagerFactory
#pragma warning restore CS9113
{
    public IAppUpdateManager Create(SemanticVersion internalVersion)
    {
        var options = new UpdateOptions { AllowVersionDowngrade = true };
#if DEBUG
        var updatePath = appPathProvider.UpdateDirectoryPath;
        var manager = new UpdateManager(
            updatePath,
            options,
            new TestVelopackLocator(
                "YouTubeMusicStreamer",
                $"{internalVersion}-debug",
                updatePath)
        );
#else
        var manager = new UpdateManager(
            new GithubSource(
                "https://github.com/XeroxDev/YouTubeMusicStreamer",
                null,
                false),
            options
        );
#endif

        return new VelopackUpdateManagerAdapter(manager);
    }
}

public sealed class VelopackUpdateManagerAdapter(UpdateManager inner) : IAppUpdateManager
{
    public bool IsInstalled => inner.IsInstalled;
    public bool IsPortable => inner.IsPortable;
    public SemanticVersion? CurrentVersion => inner.CurrentVersion;
    public VelopackAsset? UpdatePendingRestart => inner.UpdatePendingRestart;
    public Task<UpdateInfo?> CheckForUpdatesAsync() => inner.CheckForUpdatesAsync();
    public Task DownloadUpdatesAsync(UpdateInfo updateInfo) => inner.DownloadUpdatesAsync(updateInfo);
    public void ApplyUpdatesAndRestart(UpdateInfo updateInfo) => inner.ApplyUpdatesAndRestart(updateInfo);
}
