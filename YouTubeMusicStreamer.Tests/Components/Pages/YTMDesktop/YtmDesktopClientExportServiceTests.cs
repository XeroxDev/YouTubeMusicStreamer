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

using Microsoft.Extensions.DependencyInjection;
using System.Runtime.CompilerServices;
using YouTubeMusicStreamer.Components.Pages.YTMDesktop;
using YouTubeMusicStreamer.Interfaces;
using YouTubeMusicStreamer.Services.WebSocket;

namespace YouTubeMusicStreamer.Tests.Components.Pages.YTMDesktop;

public sealed class YtmDesktopClientExportServiceTests
{
    [Fact]
    public async Task ExportAsync_ReturnsNoThemeSelected_WhenThemeIsMissing()
    {
        using var services = CreateServices();
        var platform = new FakeYtmDesktopClientExportPlatform();
        var service = new YtmDesktopClientExportService(new WebSocketClientService(services), platform);

        var result = await service.ExportAsync("  ");

        Assert.Equal(YtmDesktopClientExportOutcome.NoThemeSelected, result.Outcome);
        Assert.Equal(0, platform.PickCallCount);
    }

    [Fact]
    public async Task ExportAsync_ReturnsClientNotFound_WhenThemeDoesNotExist()
    {
        using var services = CreateServices();
        var platform = new FakeYtmDesktopClientExportPlatform();
        var service = new YtmDesktopClientExportService(new WebSocketClientService(services), platform);

        var result = await service.ExportAsync("Ghost");

        Assert.Equal(YtmDesktopClientExportOutcome.ClientNotFound, result.Outcome);
        Assert.Equal("Ghost", result.ClientName);
        Assert.Equal(0, platform.PickCallCount);
    }

    [Fact]
    public async Task ExportAsync_ReturnsCancelled_WhenPickerReturnsNoPath()
    {
        using var services = CreateServices();
        var platform = new FakeYtmDesktopClientExportPlatform
        {
            PickedPath = null
        };
        var service = new YtmDesktopClientExportService(new WebSocketClientService(services), platform);

        var result = await service.ExportAsync("Modern");

        Assert.Equal(YtmDesktopClientExportOutcome.Cancelled, result.Outcome);
        Assert.Equal("Modern", result.ClientName);
        Assert.Equal(1, platform.PickCallCount);
        Assert.Empty(platform.Writes);
    }

    [Fact]
    public async Task ExportAsync_WritesBuiltClientHtml_WhenPickerReturnsPath()
    {
        using var services = CreateServices();
        var platform = new FakeYtmDesktopClientExportPlatform
        {
            PickedPath = @"C:\Exports\modern.html"
        };
        var service = new YtmDesktopClientExportService(new WebSocketClientService(services), platform);

        var result = await service.ExportAsync("Modern");

        Assert.Equal(YtmDesktopClientExportOutcome.Success, result.Outcome);
        Assert.Equal("Modern", result.ClientName);
        Assert.Equal(@"C:\Exports\modern.html", result.FilePath);
        Assert.Equal("Modern", platform.SuggestedFileName);
        var write = Assert.Single(platform.Writes);
        Assert.Equal(@"C:\Exports\modern.html", write.Path);
        Assert.False(string.IsNullOrWhiteSpace(write.Content));
    }

    [Fact]
    public async Task ExportAsync_PreservesWriteFailureExceptionContext()
    {
        using var services = CreateServices();
        var platform = new FakeYtmDesktopClientExportPlatform
        {
            PickedPath = @"C:\Exports\modern.html",
            WriteException = new IOException("Disk full")
        };
        var service = new YtmDesktopClientExportService(new WebSocketClientService(services), platform);

        var ex = await Assert.ThrowsAsync<IOException>(() => service.ExportAsync("Modern"));

        Assert.Equal("Disk full", ex.Message);
        Assert.Equal(1, platform.PickCallCount);
    }

    private static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        var webSocketService = CreateWebSocketServiceStub();
        services.AddSingleton(webSocketService);
        services.AddSingleton<YouTubeMusicStreamer.Services.App.IWidgetServerStatusSource>(webSocketService);
        return services.BuildServiceProvider();
    }

    private static WebSocketService CreateWebSocketServiceStub()
    {
        var service = (WebSocketService)RuntimeHelpers.GetUninitializedObject(typeof(WebSocketService));
        var state = new WidgetServerState(
            new WidgetServerConfiguration(8080, false, string.Empty),
            WidgetServerStatus.Stopped,
            0,
            null,
            new AudioSubsystemState(AudioCaptureStatus.Disabled, null, null),
            null);

        var stateField = typeof(WebSocketService).GetField("<State>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                         ?? throw new InvalidOperationException("WebSocketService State backing field was not found.");
        stateField.SetValue(service, state);
        return service;
    }

    private sealed class FakeYtmDesktopClientExportPlatform : IYtmDesktopClientExportPlatform
    {
        public int PickCallCount { get; private set; }
        public string? SuggestedFileName { get; private set; }
        public string? PickedPath { get; set; }
        public Exception? WriteException { get; set; }
        public List<(string Path, string Content)> Writes { get; } = [];

        public Task<string?> PickSavePathAsync(string suggestedFileName)
        {
            PickCallCount++;
            SuggestedFileName = suggestedFileName;
            return Task.FromResult(PickedPath);
        }

        public Task WriteTextAsync(string filePath, string content)
        {
            if (WriteException is not null)
                throw WriteException;

            Writes.Add((filePath, content));
            return Task.CompletedTask;
        }
    }
}
