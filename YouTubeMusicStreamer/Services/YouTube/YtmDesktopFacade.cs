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

using System.ComponentModel;
using YouTubeMusicStreamer.Services.App;
using YouTubeMusicStreamer.Services.App.Persistence;
using YouTubeMusicStreamer.Services.WebSocket;

namespace YouTubeMusicStreamer.Services.YouTube;

public sealed class YtmDesktopFacade(
    SettingsService settingsService,
    IWidgetServerController webSocketService,
    IYouTubeStatusSource youTubeService) : IDisposable
{
    private bool _initialized;

    public event EventHandler? StateChanged;

    public YtmCompanionSessionState CompanionState => youTubeService.SessionState;
    public WidgetServerState ServerState => webSocketService.State;
    public YouTubeSettingsSnapshot Settings => settingsService.GetYouTubeSettings();

    public void Initialize()
    {
        if (_initialized)
            return;

        _initialized = true;
        youTubeService.PropertyChanged += HandlePropertyChanged;
        webSocketService.StateChanged += HandleServerStateChanged;
    }

    public void Dispose()
    {
        if (!_initialized)
            return;

        youTubeService.PropertyChanged -= HandlePropertyChanged;
        webSocketService.StateChanged -= HandleServerStateChanged;
        _initialized = false;
        GC.SuppressFinalize(this);
    }

    public async Task SaveServerSettingsAsync(bool autoStartServer, int serverPort, bool allowAudioStream, string audioDeviceId)
    {
        var snapshot = new YouTubeSettingsSnapshot(
            Settings.Host,
            Settings.Port,
            autoStartServer,
            serverPort,
            allowAudioStream,
            audioDeviceId);

        await settingsService.SaveYouTubeSettingsAsync(snapshot);
        await webSocketService.ApplyConfigurationAsync(snapshot);
    }

    public async Task ToggleServerAsync()
    {
        if (webSocketService.IsRunning)
            webSocketService.Stop();
        else
            await webSocketService.StartAsync();
    }

    private void HandlePropertyChanged(object? sender, PropertyChangedEventArgs e) => StateChanged?.Invoke(this, EventArgs.Empty);

    private void HandleServerStateChanged(object? sender, WidgetServerState e) => StateChanged?.Invoke(this, EventArgs.Empty);
}
