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

using Microsoft.AspNetCore.Components;
using YouTubeMusicStreamer.Extensions;
using YouTubeMusicStreamer.Services;
using YouTubeMusicStreamer.Services.App;
using YouTubeMusicStreamer.Services.App.Persistence;
using YouTubeMusicStreamer.Services.Commands;
using YouTubeMusicStreamer.Services.Twitch;
namespace YouTubeMusicStreamer.Components.Pages.Twitch;

public partial class Twitch(TwitchSettingsPageFacade twitchSettingsPageFacade, IAppToastService toastService) : ComponentBase, IDisposable
{
    #region Global Settings Properties

    private bool _twitchSendMessageOnConnect;
    private string _twitchConnectMessage = string.Empty;
    private string _twitchCommandPrefix = string.Empty;

    #endregion

    #region Commands Properties

    private IReadOnlyList<CommandDescriptor> _commands = [];

    #endregion

    protected override void OnInitialized()
    {
        twitchSettingsPageFacade.Initialize();
        twitchSettingsPageFacade.StateChanged += OnFacadeStateChanged;
        RefreshState(resetDraftSettings: true);
    }

    public void Dispose()
    {
        twitchSettingsPageFacade.StateChanged -= OnFacadeStateChanged;
        twitchSettingsPageFacade.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task SaveGlobalSettings()
    {
        await twitchSettingsPageFacade.SaveSettingsAsync(new TwitchSettingsSnapshot(
            _twitchSendMessageOnConnect,
            _twitchConnectMessage,
            _twitchCommandPrefix,
            twitchSettingsPageFacade.Settings.BroadcasterAccount,
            twitchSettingsPageFacade.Settings.BotAccount));

        toastService.ShowSuccess("Settings saved successfully.");
    }

    private void ResetGlobalSettings(bool notify = true)
    {
        var settings = twitchSettingsPageFacade.Settings;
        _twitchSendMessageOnConnect = settings.SendMessageOnConnect;
        _twitchConnectMessage = settings.ConnectMessage;
        _twitchCommandPrefix = settings.CommandPrefix;

        if (notify)
        {
            toastService.ShowSuccess("Settings reset to previous saved state.");
        }
    }

    private static string CoalesceEmpty(string? value, string defaultValue) => value.CoalesceEmpty(defaultValue);

    private static string ToDataAttribute(string value) => value.ToLowerInvariant().Replace(" ", "-");

    private static IEnumerable<KeyValuePair<string, object>> CommandDataAttribute(string command) => [new KeyValuePair<string, object>($"data-collapse-{ToDataAttribute(command)}", "closed")];

    private void OnFacadeStateChanged(object? sender, EventArgs e)
    {
        _ = InvokeAsync(() =>
        {
            RefreshState();
            StateHasChanged();
        });
    }

    private void RefreshState(bool resetDraftSettings = false)
    {
        _commands = twitchSettingsPageFacade.Commands;

        if (resetDraftSettings)
        {
            ResetGlobalSettings(false);
        }
    }
}
