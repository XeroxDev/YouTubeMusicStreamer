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
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;
using YouTubeMusicStreamer.Services.App;
using YouTubeMusicStreamer.Services.Twitch.Interfaces;

namespace YouTubeMusicStreamer.Services.Twitch.Implementations;

public sealed class TwitchChatService(ILogger<TwitchChatService> logger, SettingsService settingsService) : ITwitchChatService
{
    private readonly TwitchClient _client = new();
    private string _channelName = string.Empty;

    public Task ConnectAsync(string username, string token)
    {
        _channelName = username;
        var credentials = new ConnectionCredentials(username, token);
        _client.OnConnected += OnConnected;
        _client.Initialize(credentials, username);
        _client.Connect();
        logger.LogInformation("Connected to channel {Channel}", username);
        return Task.CompletedTask;
    }

    private void OnConnected(object? sender, OnConnectedArgs e)
    {
        if (!settingsService.GetAppSettings().TwitchSendMessageOnConnect) return;

        var message = settingsService.GetAppSettings().TwitchConnectMessage;
        if (string.IsNullOrEmpty(message)) return;
        _client.SendMessage(string.IsNullOrWhiteSpace(_channelName) ? e.AutoJoinChannel : _channelName, message);
    }

    public Task DisconnectAsync()
    {
        _client.OnConnected -= OnConnected;
        if (!_client.IsConnected) return Task.CompletedTask;
        _client.Disconnect();
        return Task.CompletedTask;
    }

    public void SendMessage(ChannelChatMessage senderMessage, string message, bool asReply = true)
    {
        if (senderMessage.MessageId == "debug")
        {
            logger.LogDebug("[SEND MESSAGE]: {Message}", message);
            return;
        }

        try
        {
            if (asReply)
            {
                _client.SendReply(senderMessage.BroadcasterUserLogin, senderMessage.MessageId, message);
            }
            else
            {
                _client.SendMessage(senderMessage.BroadcasterUserLogin, message);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Chat send failed");
        }
    }
}