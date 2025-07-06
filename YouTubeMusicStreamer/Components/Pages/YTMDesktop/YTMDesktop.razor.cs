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

using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Blazored.Toast.Services;
using Microsoft.AspNetCore.Components;
using WinRT.Interop;
using YouTubeMusicStreamer.Interfaces;
using YouTubeMusicStreamer.Models;
using YouTubeMusicStreamer.Services;
using YouTubeMusicStreamer.Services.App;
using YouTubeMusicStreamer.Services.WebSocket;
using WebSocketService = YouTubeMusicStreamer.Services.WebSocket.WebSocketService;

namespace YouTubeMusicStreamer.Components.Pages.YTMDesktop;

public partial class YTMDesktop : ComponentBase
{
    #region Page Properties

    private readonly SettingsService _settingsService;
    private readonly WebSocketClientService _webSocketClientService;
    private readonly IToastService _toastService;
    private readonly WebSocketService _webSocketService;

    #endregion

    #region Server Settings Properties

    private bool _autoStartServer;
    private bool _allowAudioStream;
    private string _audioDeviceId = string.Empty;
    private int _serverPort;
    private string? _clientTheme;

    #endregion

    #region Blacklist Settings Properties

    private string _blacklistUrl = string.Empty;
    private string _blacklistDescription = string.Empty;

    #endregion

    public YTMDesktop(SettingsService settingsService, WebSocketClientService webSocketClientService, WebSocketService webSocketService, IToastService toastService)
    {
        _settingsService = settingsService;
        _webSocketClientService = webSocketClientService;
        _webSocketService = webSocketService;
        _toastService = toastService;
        ResetGlobalSettings(false);
    }

    private async Task SaveGlobalSettings()
    {
        await _settingsService.SaveAppSettingAsync(s =>
        {
            s.AutoStartServer = _autoStartServer;
            s.PublicPort = _serverPort;
            s.AutoStartServer = _autoStartServer;
            s.AllowAudioCapture = _allowAudioStream;
            s.AudioCaptureDevice = _audioDeviceId;
        });

        _toastService.ShowSuccess("Settings saved successfully.");
    }

    private void ResetGlobalSettings(bool notify = true)
    {
        _clientTheme = _webSocketClientService.GetAvailableClients().FirstOrDefault()?.Name ?? string.Empty;
        _autoStartServer = _settingsService.GetAppSettings().AutoStartServer;
        _serverPort = _settingsService.GetAppSettings().PublicPort;
        _autoStartServer = _settingsService.GetAppSettings().AutoStartServer;
        _allowAudioStream = _settingsService.GetAppSettings().AllowAudioCapture;
        _audioDeviceId = _settingsService.GetAppSettings().AudioCaptureDevice;

        if (notify)
        {
            _toastService.ShowSuccess("Settings reset to previous saved state.");
        }
    }

    private async Task GenerateClient()
    {
        // ask user for location to save client to
        var fileSavePicker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = _clientTheme,
            DefaultFileExtension = ".html",
            FileTypeChoices =
            {
                { "HTML File", new List<string> { ".html" } }
            }
        };

        if (MauiWinUIApplication.Current.Application.Windows[0].Handler?.PlatformView is MauiWinUIWindow window)
            InitializeWithWindow.Initialize(fileSavePicker, window.WindowHandle);

        var file = await fileSavePicker.PickSaveFileAsync();

        if (file is null || string.IsNullOrWhiteSpace(_clientTheme))
        {
            return;
        }

        var clientContent = _webSocketClientService.GetClient(_clientTheme)?.Build();
        await FileIO.WriteTextAsync(file, clientContent, UnicodeEncoding.Utf8);

        _toastService.ShowSuccess("Client generated successfully.");
    }

    private List<IWebSocketClient> GetWebSocketClients() => _webSocketClientService.GetAvailableClients();

    private IWebSocketClient? GetWebSocketClient(string? name) => string.IsNullOrWhiteSpace(name) ? null : _webSocketClientService.GetClient(name);

    private async Task ToggleServer()
    {
        if (_webSocketService.IsRunning)
            _webSocketService.Stop();
        else
            await _webSocketService.StartAsync();
    }

    private async Task RemoveBlacklistEntry(BlacklistEntry entry)
    {
        await _settingsService.SaveAppSettingAsync(s => s.Blacklist.Remove(entry));
        _toastService.ShowSuccess("Blacklist entry removed successfully.");
    }

    private async Task AddBlacklistEntry()
    {
        if (string.IsNullOrWhiteSpace(_blacklistUrl))
        {
            _toastService.ShowError("Please fill out the URL field.");
            return;
        }

        // validate URL
        if (!Uri.TryCreate(_blacklistUrl, UriKind.Absolute, out _))
        {
            _toastService.ShowError("Invalid URL provided.");
            return;
        }

        var entry = new BlacklistEntry(new Uri(_blacklistUrl), _blacklistDescription);

        await _settingsService.SaveAppSettingAsync(s => s.Blacklist.Add(entry));

        _blacklistUrl = string.Empty;
        _blacklistDescription = string.Empty;
        _toastService.ShowSuccess("Blacklist entry added successfully.");
    }
}