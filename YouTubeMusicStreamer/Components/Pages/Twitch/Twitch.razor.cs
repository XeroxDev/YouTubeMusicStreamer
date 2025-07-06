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

using Blazored.Toast.Services;
using Microsoft.AspNetCore.Components;
using YouTubeMusicStreamer.Extensions;
using YouTubeMusicStreamer.Models;
using YouTubeMusicStreamer.Services;
using YouTubeMusicStreamer.Services.App;
using YouTubeMusicStreamer.Services.Commands;
using TwitchService = YouTubeMusicStreamer.Services.Twitch.TwitchService;

namespace YouTubeMusicStreamer.Components.Pages.Twitch;

public partial class Twitch(SettingsService settingsService, TwitchService twitchService, CommandService commandService, IToastService toastService) : ComponentBase
{
    #region Global Settings Properties

    private string _twitchChatChannel = string.Empty;
    private bool _twitchSendMessageOnConnect;
    private string _twitchConnectMessage = string.Empty;
    private string _twitchCommandPrefix = string.Empty;

    #endregion

    #region Commands Properties

    private readonly IEnumerable<CommandInformation> _commands = commandService.GetAllCommandsInfo();

    #endregion

    protected override void OnInitialized()
    {
        ResetGlobalSettings(false);
    }

    private async Task SaveGlobalSettings()
    {
        await settingsService.SaveAppSettingAsync(s =>
        {
            s.TwitchChatChannel = _twitchChatChannel;
            s.TwitchSendMessageOnConnect = _twitchSendMessageOnConnect;
            s.TwitchConnectMessage = _twitchConnectMessage;
            s.TwitchCommandPrefix = _twitchCommandPrefix;
        });

        toastService.ShowSuccess("Settings saved successfully.");
    }

    private void ResetGlobalSettings(bool notify = true)
    {
        _twitchChatChannel = settingsService.GetAppSettings().TwitchChatChannel.CoalesceEmpty(twitchService.Username);
        _twitchSendMessageOnConnect = settingsService.GetAppSettings().TwitchSendMessageOnConnect;
        _twitchConnectMessage = settingsService.GetAppSettings().TwitchConnectMessage;
        _twitchCommandPrefix = settingsService.GetAppSettings().TwitchCommandPrefix;

        if (notify)
        {
            toastService.ShowSuccess("Settings reset to previous saved state.");
        }
    }

    private static string CoalesceEmpty(string? value, string defaultValue) => value.CoalesceEmpty(defaultValue);

    private static string ToDataAttribute(string value) => value.ToLowerInvariant().Replace(" ", "-");

    private static IEnumerable<KeyValuePair<string, object>> CommandDataAttribute(string command) => [new KeyValuePair<string, object>($"data-collapse-{ToDataAttribute(command)}", "closed")];
}