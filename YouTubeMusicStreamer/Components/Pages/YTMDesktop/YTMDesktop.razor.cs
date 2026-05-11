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

using System.ComponentModel;
using Microsoft.AspNetCore.Components;
using YouTubeMusicStreamer.Interfaces;
using YouTubeMusicStreamer.Models;
using YouTubeMusicStreamer.Services;
using YouTubeMusicStreamer.Services.App;
using YouTubeMusicStreamer.Services.App.Persistence;
using YouTubeMusicStreamer.Services.WebSocket;
using YouTubeMusicStreamer.Services.YouTube;

namespace YouTubeMusicStreamer.Components.Pages.YTMDesktop;

public partial class YTMDesktop : ComponentBase, IDisposable
{
    #region Page Properties

    private readonly SettingsService _settingsService;
    private readonly WebSocketClientService _webSocketClientService;
    private readonly IAppToastService _toastService;
    private readonly YtmDesktopFacade _ytmDesktopFacade;
    private readonly IYtmDesktopClientExportService _clientExportService;

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

    public YTMDesktop(
        SettingsService settingsService,
        WebSocketClientService webSocketClientService,
        IAppToastService toastService,
        YtmDesktopFacade ytmDesktopFacade,
        IYtmDesktopClientExportService clientExportService)
    {
        _settingsService = settingsService;
        _webSocketClientService = webSocketClientService;
        _toastService = toastService;
        _ytmDesktopFacade = ytmDesktopFacade;
        _clientExportService = clientExportService;
        ResetGlobalSettings(false);
    }

    protected override void OnInitialized()
    {
        _ytmDesktopFacade.Initialize();
        _ytmDesktopFacade.StateChanged += OnFacadeStateChanged;
    }

    public void Dispose()
    {
        _ytmDesktopFacade.StateChanged -= OnFacadeStateChanged;
        _ytmDesktopFacade.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task SaveGlobalSettings()
    {
        await _ytmDesktopFacade.SaveServerSettingsAsync(
            _autoStartServer,
            _serverPort,
            _allowAudioStream,
            _audioDeviceId);

        _toastService.ShowSuccess("Settings saved successfully.");
    }

    private void ResetGlobalSettings(bool notify = true)
    {
        _clientTheme = _webSocketClientService.GetAvailableClients().FirstOrDefault()?.Name ?? string.Empty;
        var settings = _settingsService.GetYouTubeSettings();
        _autoStartServer = settings.AutoStartServer;
        _serverPort = settings.PublicPort;
        _allowAudioStream = settings.AllowAudioCapture;
        _audioDeviceId = settings.AudioCaptureDevice;

        if (notify)
        {
            _toastService.ShowSuccess("Settings reset to previous saved state.");
        }
    }

    private async Task GenerateClient()
    {
        var result = await _clientExportService.ExportAsync(_clientTheme);
        if (result.Outcome is YtmDesktopClientExportOutcome.Cancelled or YtmDesktopClientExportOutcome.NoThemeSelected)
            return;

        if (result.Outcome == YtmDesktopClientExportOutcome.ClientNotFound)
        {
            _toastService.ShowError("The selected client theme is no longer available.");
            return;
        }

        _toastService.ShowSuccess("Client generated successfully.");
    }

    private IReadOnlyList<IWebSocketClient> GetWebSocketClients() => _webSocketClientService.GetAvailableClients();

    private IWebSocketClient? GetWebSocketClient(string? name) => string.IsNullOrWhiteSpace(name) ? null : _webSocketClientService.GetClient(name);

    private async Task ToggleServer()
    {
        await _ytmDesktopFacade.ToggleServerAsync();
    }

    private void OnFacadeStateChanged(object? sender, EventArgs e) => _ = InvokeAsync(StateHasChanged);

    private WidgetServerStatus EffectiveServerStatus =>
        YtmDesktopStatusPresenter.GetEffectiveServerStatus(_ytmDesktopFacade.ServerState, _ytmDesktopFacade.CompanionState);

    private string ServerStatusLabel => YtmDesktopStatusPresenter.GetServerStatusLabel(EffectiveServerStatus);

    private string ServerStatusTagClass => YtmDesktopStatusPresenter.GetServerStatusTagClass(EffectiveServerStatus);

    private bool IsAudioConfiguredButInactive =>
        YtmDesktopStatusPresenter.IsAudioConfiguredButInactive(_ytmDesktopFacade.ServerState);

    private string AudioStatusLabel => YtmDesktopStatusPresenter.GetAudioStatusLabel(_ytmDesktopFacade.ServerState);

    private string AudioStatusTagClass => YtmDesktopStatusPresenter.GetAudioStatusTagClass(_ytmDesktopFacade.ServerState);

    private string? ServerStatusMessage => YtmDesktopStatusPresenter.GetServerStatusMessage(_ytmDesktopFacade.ServerState, EffectiveServerStatus);

    private bool IsServerStatusProblem =>
        YtmDesktopStatusPresenter.IsServerStatusProblem(EffectiveServerStatus);

    private string? AudioStatusMessage => YtmDesktopStatusPresenter.GetAudioStatusMessage(_ytmDesktopFacade.ServerState);

    private bool IsAudioStatusProblem =>
        YtmDesktopStatusPresenter.IsAudioStatusProblem(_ytmDesktopFacade.ServerState);

    private async Task RemoveBlacklistEntry(BlacklistEntry entry)
    {
        await _settingsService.UpdateBlacklistEntriesAsync(entries => entries.RemoveAll(x => x.Url == entry.Url && x.Description == entry.Description));
        _toastService.ShowSuccess("Blacklist entry removed successfully.");
    }

    private async Task AddBlacklistEntry()
    {
        var result = YtmDesktopBlacklistState.TryCreateEntry(_blacklistUrl, _blacklistDescription);
        if (result.Error == BlacklistEntryValidationError.MissingUrl)
        {
            _toastService.ShowError("Please fill out the URL field.");
            return;
        }

        if (result.Error == BlacklistEntryValidationError.InvalidUrl)
        {
            _toastService.ShowError("Invalid URL provided.");
            return;
        }

        await _settingsService.UpdateBlacklistEntriesAsync(entries => entries.Add(result.Entry!));

        _blacklistUrl = string.Empty;
        _blacklistDescription = string.Empty;
        _toastService.ShowSuccess("Blacklist entry added successfully.");
    }
}
