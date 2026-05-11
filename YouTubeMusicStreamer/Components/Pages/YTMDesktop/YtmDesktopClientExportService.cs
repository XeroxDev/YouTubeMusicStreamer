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

using System.Text;
using Windows.Storage.Pickers;
using WinRT.Interop;
using YouTubeMusicStreamer.Interfaces;
using YouTubeMusicStreamer.Services.WebSocket;

namespace YouTubeMusicStreamer.Components.Pages.YTMDesktop;

public enum YtmDesktopClientExportOutcome
{
    Success,
    Cancelled,
    NoThemeSelected,
    ClientNotFound
}

public sealed record YtmDesktopClientExportResult(
    YtmDesktopClientExportOutcome Outcome,
    string? FilePath = null,
    string? ClientName = null);

public interface IYtmDesktopClientExportService
{
    Task<YtmDesktopClientExportResult> ExportAsync(string? clientTheme);
}

internal interface IYtmDesktopClientExportPlatform
{
    Task<string?> PickSavePathAsync(string suggestedFileName);
    Task WriteTextAsync(string filePath, string content);
}

internal sealed class MauiYtmDesktopClientExportPlatform : IYtmDesktopClientExportPlatform
{
    public async Task<string?> PickSavePathAsync(string suggestedFileName)
    {
        var fileSavePicker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = suggestedFileName,
            DefaultFileExtension = ".html",
            FileTypeChoices =
            {
                { "HTML File", [".html"] }
            }
        };

        if (MauiWinUIApplication.Current.Application.Windows[0].Handler?.PlatformView is MauiWinUIWindow window)
            InitializeWithWindow.Initialize(fileSavePicker, window.WindowHandle);

        var file = await fileSavePicker.PickSaveFileAsync();
        return file?.Path;
    }

    public Task WriteTextAsync(string filePath, string content) =>
        File.WriteAllTextAsync(filePath, content, Encoding.UTF8);
}

internal sealed class YtmDesktopClientExportService(
    WebSocketClientService webSocketClientService,
    IYtmDesktopClientExportPlatform platform) : IYtmDesktopClientExportService
{
    public async Task<YtmDesktopClientExportResult> ExportAsync(string? clientTheme)
    {
        if (string.IsNullOrWhiteSpace(clientTheme))
            return new(YtmDesktopClientExportOutcome.NoThemeSelected);

        var client = webSocketClientService.GetClient(clientTheme);
        if (client is null)
            return new(YtmDesktopClientExportOutcome.ClientNotFound, ClientName: clientTheme);

        var filePath = await platform.PickSavePathAsync(clientTheme);
        if (string.IsNullOrWhiteSpace(filePath))
            return new(YtmDesktopClientExportOutcome.Cancelled, ClientName: clientTheme);

        await platform.WriteTextAsync(filePath, client.Build());
        return new(YtmDesktopClientExportOutcome.Success, filePath, client.Name);
    }
}
