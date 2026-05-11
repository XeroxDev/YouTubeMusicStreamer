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

using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging.Abstractions;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;
using YouTubeMusicStreamer.Services.App;
using YouTubeMusicStreamer.Services.App.Persistence;
using YouTubeMusicStreamer.Services.Twitch.Implementations;
using YouTubeMusicStreamer.Services.Twitch.Interfaces;
using YouTubeMusicStreamer.Tests.TestSupport;

namespace YouTubeMusicStreamer.Tests.Services.Twitch;

public sealed class TwitchChatServiceTests
{
    [Fact]
    public async Task ConnectAsync_InitializesClient_JoinsNormalizedChannel_AndSendsConfiguredConnectMessage()
    {
        var initialClient = new FakeTwitchChatClient();
        var connectClient = new FakeTwitchChatClient
        {
            JoinEventChannel = "#StreamerChannel"
        };
        var service = CreateService(
            new FakeTwitchChatClientFactory(initialClient, connectClient),
            new TwitchSettingsSnapshot(true, "Hello chat", "!", null, null));

        await service.ConnectAsync("bot-user", "access-token", "streamerchannel");

        Assert.Equal(("bot-user", "access-token", "streamerchannel"), connectClient.InitializedWith);
        Assert.Equal(["streamerchannel"], connectClient.JoinedChannels);
        Assert.Equal([("streamerchannel", "Hello chat")], connectClient.SentMessages);
    }

    [Fact]
    public async Task DisconnectAsync_DisconnectsOnlyWhenClientIsConnected()
    {
        var initialClient = new FakeTwitchChatClient { IsConnectedValue = true };
        var replacementClient = new FakeTwitchChatClient();
        var service = CreateService(new FakeTwitchChatClientFactory(initialClient, replacementClient));

        await service.DisconnectAsync();
        await service.DisconnectAsync();

        Assert.Equal(1, initialClient.DisconnectCallCount);
        Assert.Equal(0, replacementClient.DisconnectCallCount);
    }

    [Fact]
    public async Task SendMessage_UsesReplyByDefault_AndUsesConnectedChannel()
    {
        var initialClient = new FakeTwitchChatClient();
        var connectClient = new FakeTwitchChatClient();
        var service = CreateService(new FakeTwitchChatClientFactory(initialClient, connectClient));
        await service.ConnectAsync("bot-user", "access-token", "streamerchannel");

        service.SendMessage(CreateChatMessage("msg-1", "fallback"), "hello");

        Assert.Equal([("streamerchannel", "msg-1", "hello")], connectClient.SentReplies);
        Assert.Empty(connectClient.SentMessages);
    }

    [Fact]
    public void SendMessage_UsesSenderBroadcaster_WhenNoChannelWasConnected()
    {
        var initialClient = new FakeTwitchChatClient();
        var service = CreateService(new FakeTwitchChatClientFactory(initialClient));

        service.SendMessage(CreateChatMessage("msg-2", "fallbackchannel"), "hello", asReply: false);

        Assert.Equal([("fallbackchannel", "hello")], initialClient.SentMessages);
    }

    [Fact]
    public void SendMessage_DoesNothingForDebugMessages()
    {
        var initialClient = new FakeTwitchChatClient();
        var service = CreateService(new FakeTwitchChatClientFactory(initialClient));

        service.SendMessage(CreateChatMessage("debug", "fallbackchannel"), "hello");

        Assert.Empty(initialClient.SentReplies);
        Assert.Empty(initialClient.SentMessages);
    }

    [Fact]
    public void SendMessage_SwallowsTransportErrors()
    {
        var initialClient = new FakeTwitchChatClient
        {
            SendReplyException = new InvalidOperationException("send failed")
        };
        var service = CreateService(new FakeTwitchChatClientFactory(initialClient));

        service.SendMessage(CreateChatMessage("msg-3", "fallback"), "hello");

        Assert.Equal(1, initialClient.SendReplyAttemptCount);
    }

    private static TwitchChatService CreateService(ITwitchChatClientFactory factory, TwitchSettingsSnapshot? twitchSettings = null)
    {
        var settings = SettingsServiceTestSupport.CreateWithThrowingDb(
            twitchSettings: twitchSettings ?? new TwitchSettingsSnapshot(false, string.Empty, "!", null, null));
        return new TwitchChatService(NullLogger<TwitchChatService>.Instance, settings, factory);
    }

    private static ChannelChatMessage CreateChatMessage(string messageId, string broadcasterLogin)
    {
        var message = (ChannelChatMessage)RuntimeHelpers.GetUninitializedObject(typeof(ChannelChatMessage));
        SettingsServiceTestSupport.SetAutoProperty(message, nameof(ChannelChatMessage.MessageId), messageId);
        SettingsServiceTestSupport.SetAutoProperty(message, nameof(ChannelChatMessage.BroadcasterUserLogin), broadcasterLogin);
        return message;
    }

    private sealed class FakeTwitchChatClientFactory(params FakeTwitchChatClient[] clients) : ITwitchChatClientFactory
    {
        private readonly Queue<FakeTwitchChatClient> _clients = new(clients);
        private FakeTwitchChatClient? _lastClient;

        public ITwitchChatClient Create()
        {
            if (_clients.Count > 0)
            {
                _lastClient = _clients.Dequeue();
                return _lastClient;
            }

            return _lastClient ??= new FakeTwitchChatClient();
        }
    }

    private sealed class FakeTwitchChatClient : ITwitchChatClient
    {
        public event EventHandler<TwitchChatConnectedEventArgs>? Connected;
        public event EventHandler<TwitchChatJoinedChannelEventArgs>? JoinedChannel;

        public bool IsConnected => IsConnectedValue;
        public bool IsConnectedValue { get; set; }
        public string JoinEventChannel { get; set; } = "streamerchannel";
        public (string Username, string Token, string Channel)? InitializedWith { get; private set; }
        public List<string> JoinedChannels { get; } = [];
        public List<(string Channel, string MessageId, string Message)> SentReplies { get; } = [];
        public List<(string Channel, string Message)> SentMessages { get; } = [];
        public int DisconnectCallCount { get; private set; }
        public int SendReplyAttemptCount { get; private set; }
        public Exception? SendReplyException { get; set; }

        public void Initialize(string username, string token, string channelLogin) =>
            InitializedWith = (username, token, channelLogin);

        public void Connect()
        {
            IsConnectedValue = true;
            Connected?.Invoke(this, new TwitchChatConnectedEventArgs());
        }

        public void Disconnect()
        {
            DisconnectCallCount++;
            IsConnectedValue = false;
        }

        public void JoinChannel(string channelName)
        {
            JoinedChannels.Add(channelName);
            JoinedChannel?.Invoke(this, new TwitchChatJoinedChannelEventArgs(JoinEventChannel));
        }

        public void SendReply(string channelName, string messageId, string message)
        {
            SendReplyAttemptCount++;
            if (SendReplyException is not null)
                throw SendReplyException;

            SentReplies.Add((channelName, messageId, message));
        }

        public void SendMessage(string channelName, string message) => SentMessages.Add((channelName, message));
    }
}
