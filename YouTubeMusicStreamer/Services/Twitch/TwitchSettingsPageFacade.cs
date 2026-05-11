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

using YouTubeMusicStreamer.Services.App;
using YouTubeMusicStreamer.Services.App.Persistence;
using YouTubeMusicStreamer.Services.Commands;

namespace YouTubeMusicStreamer.Services.Twitch;

public sealed class TwitchSettingsPageFacade(SettingsService settingsService, CommandService commandService) : IDisposable
{
    private bool _initialized;

    public event EventHandler? StateChanged;

    public TwitchSettingsSnapshot Settings { get; private set; } = new(true, string.Empty, "!", null, null);
    public IReadOnlyList<CommandDescriptor> Commands { get; private set; } = [];

    public void Initialize()
    {
        if (_initialized)
            return;

        _initialized = true;
        Settings = settingsService.GetTwitchSettings();
        Commands = commandService.GetAllCommandsInfo();
        settingsService.TwitchSettingsChanged += HandleTwitchSettingsChanged;
        settingsService.CommandConfigurationsChanged += HandleCommandConfigurationsChanged;
    }

    public async Task SaveSettingsAsync(TwitchSettingsSnapshot snapshot)
    {
        await settingsService.SaveTwitchSettingsAsync(snapshot);
    }

    public void Dispose()
    {
        if (!_initialized)
            return;

        settingsService.TwitchSettingsChanged -= HandleTwitchSettingsChanged;
        settingsService.CommandConfigurationsChanged -= HandleCommandConfigurationsChanged;
        _initialized = false;
        GC.SuppressFinalize(this);
    }

    private void HandleTwitchSettingsChanged(TwitchSettingsSnapshot snapshot)
    {
        Settings = snapshot;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void HandleCommandConfigurationsChanged(IReadOnlyDictionary<string, CommandConfigurationSnapshot> snapshots)
    {
        Commands = commandService.GetAllCommandsInfo();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
