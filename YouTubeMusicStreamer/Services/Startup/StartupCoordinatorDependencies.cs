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

using Microsoft.Maui.Controls;

namespace YouTubeMusicStreamer.Services.Startup;

public sealed record StartupCommandDescriptor(string Trigger, string Description, bool IsEnabled);

public interface IStartupRuntimeServices
{
    Task InitializeSettingsAsync();
    Task PreloadSensitiveSettingsAsync();
    Task InitializeCommandsAsync();
    IReadOnlyList<StartupCommandDescriptor> ListCommands();
    Task InitializeVersionAsync();
    Exception? LastVersionCheckError { get; }
    Task InitializeYouTubeAsync();
    Task InitializeTwitchAsync();
    bool ShouldAutoStartWidgetServer { get; }
    Task StartWidgetServerAsync();
    void StopWidgetServer();
    void ValidateDevelopmentConfiguration();
}

public interface IStartupPlatform
{
    event EventHandler Activated;
    event EventHandler ProcessExit;
    void EnsureDirectoryExists(string path);
    bool IsWindowReady(Window? window);
    void BringWindowToFront(Window? window);
}

public interface IInstanceCommandServerFactory
{
    IInstanceCommandServer Create();
}

public interface IInstanceCommandServer : IAsyncDisposable
{
    Task<InstanceCommand?> WaitForCommandAsync(CancellationToken cancellationToken);
    Task SendResultAsync(InstanceCommandResult result, CancellationToken cancellationToken);
}

public interface IStartupStateStore
{
    void Persist(InstanceHealthState health);
}
