// This file is part of YouTubeMusicStreamer.
// Copyright (C) 2025 Dominic Ris
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

using Microsoft.Extensions.Logging;
using Microsoft.Windows.AppLifecycle;
using WinRT.Interop;
using YouTubeMusicStreamer.Services;
using YouTubeMusicStreamer.Services.App;
using YouTubeMusicStreamer.Services.Commands;
using YouTubeMusicStreamer.Utils;
using AudioService = YouTubeMusicStreamer.Services.App.AudioService;
using WebSocketService = YouTubeMusicStreamer.Services.WebSocket.WebSocketService;

namespace YouTubeMusicStreamer;

public partial class App
{
    private readonly ILogger<App> _logger;
    private readonly CommandService _commandService;
    private readonly SettingsService _settingsService;
    private readonly WebSocketService _webSocketService;

    public App(ILogger<App> logger, CommandService commandService, SettingsService settingsService, WebSocketService webSocketService)
    {
        _logger = logger;
        _commandService = commandService;
        _settingsService = settingsService;
        _webSocketService = webSocketService;
        logger.LogInformation("Initializing App");

        InitializeComponent();

        if (MainThread.IsMainThread)
        {
            MainThreadInitializer();
        }
        else
        {
            MainThread.BeginInvokeOnMainThread(MainThreadInitializer);
        }

        logger.LogInformation("App initialized");
    }

    private void MainThreadInitializer()
    {
        _logger.LogInformation("Initializing MainThread");
        
        AppInstance.GetCurrent().Activated += (_, args) =>
        {
            try
            {
                var mauiWindow = Current?.Windows[0];
                if (mauiWindow?.Handler?.PlatformView is not Microsoft.UI.Xaml.Window winUiWindow)
                {
                    _logger.LogError("Unable to get WinUI Window for activation");
                    return;
                }

                var hWnd = WindowNative.GetWindowHandle(winUiWindow);

                NativeMethods.ShowWindowAsync(hWnd, NativeMethods.SwRestore);
                NativeMethods.SetForegroundWindow(hWnd);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while activating app instance");
            }
        };

        foreach (var (trigger, description, isEnabled) in _commandService.ListCommands())
        {
            _logger.LogInformation("Found command: {Trigger} - {Description} | Enabled: {IsEnabled}", trigger, description, isEnabled);
        }

        AudioService.GetDevices();

        if (_settingsService.GetAppSettings().AutoStartServer)
            _ = _webSocketService.StartAsync();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new MainPage())
        {
            Title = AppInfo.Current.Name,
            MinimumHeight = 500,
            MinimumWidth = 900,
            Width = 1100,
            Height = 600
        };
    }
}