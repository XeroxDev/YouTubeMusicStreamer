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
using Microsoft.UI.Dispatching;
using Microsoft.Windows.AppLifecycle;
using Velopack;
using WinRT;
using YouTubeMusicStreamer.Utils;
using Application = Microsoft.UI.Xaml.Application;

namespace YouTubeMusicStreamer;

public static class Program
{
    [STAThread]
    public static int Main()
    {
        VelopackApp.Build()
            .OnFirstRun(v => AppUtils.FirstInstall = v)
            .OnRestarted(v => AppUtils.Updated = v)
            .Run();
        var logger = CreateLogger("Program");
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            logger.LogError(args.ExceptionObject as Exception, "Unhandled exception: {Message}", (args.ExceptionObject as Exception)?.Message);
        };
        var instance = AppInstance.FindOrRegisterForKey(AppUtils.AppName);
        if (!instance.IsCurrent)
        {
            logger.LogInformation("Redirecting activation to current instance");
            var first = AppInstance.GetCurrent();
            instance.RedirectActivationToAsync(first.GetActivatedEventArgs())
                .GetAwaiter().GetResult();
            return 0;
        }

        logger.LogInformation("Initializing App");
        ComWrappersSupport.InitializeComWrappers();
        Application.Start(_ =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
#pragma warning disable CA1806
            // ReSharper disable once ObjectCreationAsStatement
            new WinUI.App();
#pragma warning restore CA1806
        });

        return 0;
    }

    private static ILogger CreateLogger(string name) => LoggingConfig.CreateLoggerFactory().CreateLogger(name);
}