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
using YouTubeMusicStreamer.Services.Startup;
using YouTubeMusicStreamer.Utils;

namespace YouTubeMusicStreamer;

public partial class App
{
    private readonly ILogger<App> _logger;
    private readonly IStartupCoordinator _startupCoordinator;

    public App(ILogger<App> logger, IStartupCoordinator startupCoordinator)
    {
        _logger = logger;
        _startupCoordinator = startupCoordinator;
        logger.LogInformation("Initializing App");

        InitializeComponent();
        _startupCoordinator.InitializePrimaryInstanceInfrastructure();

        logger.LogInformation("App initialized");
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new MainPage())
        {
            Title = AppInfo.Current.Name,
            MinimumHeight = 500,
            MinimumWidth = 900,
            Width = 1100,
            Height = 600
        };

        window.Destroying += (_, _) => _startupCoordinator.OnWindowDestroying();
        _startupCoordinator.OnWindowCreated(window);
        return window;
    }
}
