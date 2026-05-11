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

public sealed class TwitchChatService(ILogger<TwitchChatService> logger, SettingsService settingsService, ITwitchChatClientFactory chatClientFactory) : ITwitchChatService
{
    private ITwitchChatClient _client = chatClientFactory.Create();
    private string _channelName = string.Empty;
    private readonly HashSet<string> _joinedChannels = [];

    public async Task ConnectAsync(string username, string token, string channelLogin)
    {
        await DisconnectAsync();
        _client = chatClientFactory.Create();

        _channelName = channelLogin;
        _joinedChannels.Clear();
        var connected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var joined = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnConnected(object? sender, TwitchChatConnectedEventArgs e)
        {
            connected.TrySetResult();
            if (!HasJoinedChannel(_channelName))
            {
                _client.JoinChannel(_channelName);
            }
        }

        void OnJoinedChannel(object? sender, TwitchChatJoinedChannelEventArgs e)
        {
            var joinedChannel = NormalizeChannel(e.Channel);
            _joinedChannels.Add(joinedChannel);
            if (joinedChannel == NormalizeChannel(_channelName))
                joined.TrySetResult();
        }

        _client.Connected += OnConnected;
        _client.JoinedChannel += OnJoinedChannel;
        _client.Initialize(username, token, channelLogin);
        _client.Connect();

        try
        {
            await WaitAsync(connected.Task, TimeSpan.FromSeconds(10), "Timed out while connecting to Twitch chat.");
            await WaitAsync(joined.Task, TimeSpan.FromSeconds(10), "Timed out while joining the Twitch chat channel.");
            SendConnectMessageIfConfigured();
            logger.LogInformation("Connected to channel {Channel} as {Username}", channelLogin, username);
        }
        finally
        {
            _client.Connected -= OnConnected;
            _client.JoinedChannel -= OnJoinedChannel;
        }
    }

    public Task DisconnectAsync()
    {
        _joinedChannels.Clear();
        if (_client.IsConnected)
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
            var targetChannel = string.IsNullOrWhiteSpace(_channelName) ? senderMessage.BroadcasterUserLogin : _channelName;
            if (asReply)
            {
                _client.SendReply(targetChannel, senderMessage.MessageId, message);
            }
            else
            {
                _client.SendMessage(targetChannel, message);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Chat send failed");
        }
    }

    private void SendConnectMessageIfConfigured()
    {
        var twitchSettings = settingsService.GetTwitchSettings();
        if (!twitchSettings.SendMessageOnConnect)
            return;

        var message = twitchSettings.ConnectMessage;
        if (string.IsNullOrWhiteSpace(message))
            return;

        _client.SendMessage(_channelName, message);
    }

    private bool HasJoinedChannel(string channelName)
    {
        var normalizedTarget = NormalizeChannel(channelName);
        return _joinedChannels.Contains(normalizedTarget);
    }

    private static string NormalizeChannel(string? channelName) =>
        (channelName ?? string.Empty).Trim().TrimStart('#').ToLowerInvariant();

    private static async Task WaitAsync(Task task, TimeSpan timeout, string errorMessage)
    {
        var completed = await Task.WhenAny(task, Task.Delay(timeout));
        if (completed != task)
            throw new TimeoutException(errorMessage);

        await task;
    }
}

public sealed class TwitchChatClientFactory : ITwitchChatClientFactory
{
    public ITwitchChatClient Create() => new TwitchLibChatClientAdapter(new TwitchClient());
}

internal sealed class TwitchLibChatClientAdapter : ITwitchChatClient
{
    private readonly TwitchClient _client;

    public TwitchLibChatClientAdapter(TwitchClient client)
    {
        _client = client;
        _client.OnConnected += HandleConnected;
        _client.OnJoinedChannel += HandleJoinedChannel;
    }

    public event EventHandler<TwitchChatConnectedEventArgs>? Connected;
    public event EventHandler<TwitchChatJoinedChannelEventArgs>? JoinedChannel;

    public bool IsConnected => _client.IsConnected;

    public void Initialize(string username, string token, string channelLogin)
    {
        var credentials = new ConnectionCredentials(username, token);
        _client.Initialize(credentials, channelLogin);
    }

    public void Connect() => _client.Connect();

    public void Disconnect() => _client.Disconnect();

    public void JoinChannel(string channelName) => _client.JoinChannel(channelName);

    public void SendReply(string channelName, string messageId, string message) => _client.SendReply(channelName, messageId, message);

    public void SendMessage(string channelName, string message) => _client.SendMessage(channelName, message);

    private void HandleConnected(object? sender, OnConnectedArgs args) => Connected?.Invoke(this, new TwitchChatConnectedEventArgs());

    private void HandleJoinedChannel(object? sender, OnJoinedChannelArgs args) => JoinedChannel?.Invoke(this, new TwitchChatJoinedChannelEventArgs(args.Channel));
}
