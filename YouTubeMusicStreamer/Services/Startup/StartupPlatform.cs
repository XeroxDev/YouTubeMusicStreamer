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
using Microsoft.Windows.AppLifecycle;
using WinRT.Interop;
using YouTubeMusicStreamer.Utils;

namespace YouTubeMusicStreamer.Services.Startup;

public sealed class StartupPlatform : IStartupPlatform
{
    public event EventHandler Activated = delegate { };
    public event EventHandler ProcessExit = delegate { };

    public StartupPlatform()
    {
        AppInstance.GetCurrent().Activated += HandleActivated;
        AppDomain.CurrentDomain.ProcessExit += HandleProcessExit;
    }

    public void EnsureDirectoryExists(string path) => Directory.CreateDirectory(path);

    public bool IsWindowReady(Window? window)
    {
        if (window?.Handler?.PlatformView is not Microsoft.UI.Xaml.Window winUiWindow)
            return false;

        return WindowNative.GetWindowHandle(winUiWindow) != IntPtr.Zero;
    }

    public void BringWindowToFront(Window? window)
    {
        if (window?.Handler?.PlatformView is not Microsoft.UI.Xaml.Window winUiWindow)
            return;

        var hWnd = WindowNative.GetWindowHandle(winUiWindow);
        if (hWnd == IntPtr.Zero)
            return;

        NativeMethods.ShowWindowAsync(hWnd, NativeMethods.SwRestore);
        NativeMethods.SetForegroundWindow(hWnd);
    }

    private void HandleActivated(object? sender, AppActivationArguments args) => Activated(this, EventArgs.Empty);
    private void HandleProcessExit(object? sender, EventArgs args) => ProcessExit(this, EventArgs.Empty);
}
