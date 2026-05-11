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

using YouTubeMusicStreamer.Services.Twitch;
using YouTubeMusicStreamer.Services.App.Diagnostics;

namespace YouTubeMusicStreamer.Services.App;

public sealed class AppNotificationBridge(
    IAppToastService notificationService,
    IAppDiagnosticsService diagnosticsService) : IDisposable
{
    private bool _initialized;
    private bool _disposed;

    public void Initialize()
    {
        if (_initialized || _disposed)
            return;

        _initialized = true;
        diagnosticsService.DiagnosticRecorded += HandleDiagnosticRecorded;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_initialized)
        {
            diagnosticsService.DiagnosticRecorded -= HandleDiagnosticRecorded;
        }

        GC.SuppressFinalize(this);
    }

    private void HandleDiagnosticRecorded(object? sender, AppDiagnostic diagnostic)
    {
        if (diagnostic.Visibility != AppDiagnosticVisibility.Toast)
            return;

        switch (diagnostic.Severity)
        {
            case AppDiagnosticSeverity.Info:
                notificationService.ShowInfo(diagnostic.Summary);
                break;
            case AppDiagnosticSeverity.Warning:
                notificationService.ShowWarning(diagnostic.Summary);
                break;
            case AppDiagnosticSeverity.Error:
                notificationService.ShowError(diagnostic.Summary);
                break;
            default:
                notificationService.ShowInfo(diagnostic.Summary);
                break;
        }
    }
}
