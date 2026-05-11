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
using TwitchLib.EventSub.Websockets.Core.EventArgs;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Channel;
using YouTubeMusicStreamer.Services.App;
using YouTubeMusicStreamer.Services.Twitch.Implementations;
using YouTubeMusicStreamer.Services.Twitch.Interfaces;

namespace YouTubeMusicStreamer.Tests.Services.Twitch;

public sealed class TwitchEventSubServiceTests
{
    [Fact]
    public async Task StartAsync_StoresChannelAndToken_AndConnectsTransport()
    {
        var transport = new FakeEventSubTransport();
        var subscriptionFactory = new FakeEventSubSubscriptionClientFactory();
        var delay = new FakeAsyncDelay();
        var service = CreateService(transport, subscriptionFactory, delay);

        await service.StartAsync("channel-1", "token-1");

        Assert.Equal(1, transport.ConnectCallCount);
        Assert.Equal("channel-1", GetPrivateField<string?>(service, "_channelId"));
        Assert.Equal("token-1", GetPrivateField<string?>(service, "_accessToken"));
    }

    [Fact]
    public async Task Connected_SubscribesToChatAndRewards_AndTracksOwnedSubscriptionIds()
    {
        var transport = new FakeEventSubTransport { SessionId = "session-1" };
        var subscriptionClient = new FakeEventSubSubscriptionClient();
        subscriptionClient.EnqueueCreateResult("channel.chat.message", ["chat-sub"]);
        subscriptionClient.EnqueueCreateResult("channel.channel_points_custom_reward_redemption.add", ["reward-sub"]);
        var factory = new FakeEventSubSubscriptionClientFactory(subscriptionClient);
        var service = CreateService(transport, factory, new FakeAsyncDelay());

        await service.StartAsync("channel-1", "token-1");
        await transport.RaiseConnectedAsync(CreateConnectedArgs());

        Assert.Equal(2, subscriptionClient.CreateCalls.Count);
        Assert.Equal("channel.chat.message", subscriptionClient.CreateCalls[0].Type);
        Assert.Equal("channel.channel_points_custom_reward_redemption.add", subscriptionClient.CreateCalls[1].Type);
        Assert.Equal("channel-1", subscriptionClient.CreateCalls[0].Conditions["broadcaster_user_id"]);
        Assert.Equal("channel-1", subscriptionClient.CreateCalls[0].Conditions["user_id"]);
        Assert.Equal(["chat-sub", "reward-sub"], GetOwnedSubscriptionIds(service));
    }

    [Fact]
    public async Task StopAsync_DeletesOnlyOwnedSubscriptions_AndDisconnectsTransport()
    {
        var transport = new FakeEventSubTransport { SessionId = "session-1" };
        var subscriptionClient = new FakeEventSubSubscriptionClient();
        subscriptionClient.EnqueueCreateResult("channel.chat.message", ["chat-sub"]);
        subscriptionClient.EnqueueCreateResult("channel.channel_points_custom_reward_redemption.add", ["reward-sub"]);
        var factory = new FakeEventSubSubscriptionClientFactory(subscriptionClient);
        var service = CreateService(transport, factory, new FakeAsyncDelay());

        await service.StartAsync("channel-1", "token-1");
        await transport.RaiseConnectedAsync(CreateConnectedArgs());
        await service.StopAsync();

        Assert.Equal(1, transport.DisconnectCallCount);
        Assert.Equal(["chat-sub", "reward-sub"], subscriptionClient.DeleteCalls);
        Assert.Empty(GetOwnedSubscriptionIds(service));
    }

    [Fact]
    public async Task Disconnected_RetriesReconnectUntilSuccess_WithoutFullRestart()
    {
        var transport = new FakeEventSubTransport { SessionId = "session-1" };
        transport.ReconnectResults.Enqueue(false);
        transport.ReconnectResults.Enqueue(false);
        transport.ReconnectResults.Enqueue(true);
        var subscriptionClient = new FakeEventSubSubscriptionClient();
        var factory = new FakeEventSubSubscriptionClientFactory(subscriptionClient);
        var delay = new FakeAsyncDelay();
        var service = CreateService(transport, factory, delay);

        await service.StartAsync("channel-1", "token-1");
        await transport.RaiseDisconnectedAsync();

        Assert.Equal(3, transport.ReconnectCallCount);
        Assert.Equal(2, delay.Delays.Count);
        Assert.Equal(1, transport.ConnectCallCount);
        Assert.Equal(0, transport.DisconnectCallCount);
    }

    [Fact]
    public async Task Disconnected_PerformsFullRestartAfterFiveFailedReconnects()
    {
        var transport = new FakeEventSubTransport { SessionId = "session-1" };
        for (var i = 0; i < 5; i++)
            transport.ReconnectResults.Enqueue(false);

        var subscriptionClient = new FakeEventSubSubscriptionClient();
        subscriptionClient.EnqueueCreateResult("channel.chat.message", ["chat-sub"]);
        subscriptionClient.EnqueueCreateResult("channel.channel_points_custom_reward_redemption.add", ["reward-sub"]);
        subscriptionClient.EnqueueCreateResult("channel.chat.message", ["chat-sub"]);
        subscriptionClient.EnqueueCreateResult("channel.channel_points_custom_reward_redemption.add", ["reward-sub"]);
        var factory = new FakeEventSubSubscriptionClientFactory(subscriptionClient);
        var delay = new FakeAsyncDelay();
        var service = CreateService(transport, factory, delay);

        await service.StartAsync("channel-1", "token-1");
        await transport.RaiseConnectedAsync(CreateConnectedArgs());
        await transport.RaiseDisconnectedAsync();

        Assert.Equal(5, transport.ReconnectCallCount);
        Assert.Equal(5, delay.Delays.Count);
        Assert.Equal(2, transport.ConnectCallCount);
        Assert.Equal(1, transport.DisconnectCallCount);
        Assert.Equal(["chat-sub", "reward-sub"], subscriptionClient.DeleteCalls);
    }

    [Fact]
    public async Task Reconnected_DeletesOwnedSubscriptions_AndResubscribes()
    {
        var transport = new FakeEventSubTransport { SessionId = "session-1" };
        var subscriptionClient = new FakeEventSubSubscriptionClient();
        subscriptionClient.EnqueueCreateResult("channel.chat.message", ["chat-sub-1"]);
        subscriptionClient.EnqueueCreateResult("channel.channel_points_custom_reward_redemption.add", ["reward-sub-1"]);
        subscriptionClient.EnqueueCreateResult("channel.chat.message", ["chat-sub-2"]);
        subscriptionClient.EnqueueCreateResult("channel.channel_points_custom_reward_redemption.add", ["reward-sub-2"]);
        var factory = new FakeEventSubSubscriptionClientFactory(subscriptionClient);
        var service = CreateService(transport, factory, new FakeAsyncDelay());

        await service.StartAsync("channel-1", "token-1");
        await transport.RaiseConnectedAsync(CreateConnectedArgs());
        subscriptionClient.CreateCalls.Clear();
        subscriptionClient.DeleteCalls.Clear();

        await transport.RaiseReconnectedAsync();

        Assert.Equal(["chat-sub-1", "reward-sub-1"], subscriptionClient.DeleteCalls);
        Assert.Equal(2, subscriptionClient.CreateCalls.Count);
        Assert.Equal(["chat-sub-2", "reward-sub-2"], GetOwnedSubscriptionIds(service));
    }

    [Fact]
    public async Task TransportEvents_AreForwardedToConsumers()
    {
        var transport = new FakeEventSubTransport();
        var service = CreateService(transport, new FakeEventSubSubscriptionClientFactory(), new FakeAsyncDelay());
        var chatArgs = CreateChannelChatMessageArgs();
        var rewardArgs = CreateRewardRedeemedArgs();
        ChannelChatMessageArgs? observedChat = null;
        ChannelPointsCustomRewardRedemptionArgs? observedReward = null;

        service.OnChatMessage += (_, args) => observedChat = args;
        service.OnRewardRedeemed += (_, args) => observedReward = args;

        await transport.RaiseChatMessageAsync(chatArgs);
        await transport.RaiseRewardRedeemedAsync(rewardArgs);

        Assert.Same(chatArgs, observedChat);
        Assert.Same(rewardArgs, observedReward);
    }

    private static TwitchEventSubService CreateService(
        ITwitchEventSubTransport transport,
        ITwitchEventSubSubscriptionClientFactory subscriptionClientFactory,
        IAsyncDelay asyncDelay) =>
        new(
            transport,
            NullLogger<TwitchEventSubService>.Instance,
            subscriptionClientFactory,
            asyncDelay);

    private static WebsocketConnectedArgs CreateConnectedArgs() =>
        (WebsocketConnectedArgs)RuntimeHelpers.GetUninitializedObject(typeof(WebsocketConnectedArgs));

    private static ChannelChatMessageArgs CreateChannelChatMessageArgs() =>
        (ChannelChatMessageArgs)RuntimeHelpers.GetUninitializedObject(typeof(ChannelChatMessageArgs));

    private static ChannelPointsCustomRewardRedemptionArgs CreateRewardRedeemedArgs() =>
        (ChannelPointsCustomRewardRedemptionArgs)RuntimeHelpers.GetUninitializedObject(typeof(ChannelPointsCustomRewardRedemptionArgs));

    private static IReadOnlyList<string> GetOwnedSubscriptionIds(TwitchEventSubService service)
    {
        var field = typeof(TwitchEventSubService).GetField("_ownedSubscriptionIds", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException("_ownedSubscriptionIds field was not found.");
        return ((HashSet<string>)field.GetValue(service)!).OrderBy(static id => id).ToList();
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException($"Field '{fieldName}' was not found.");
        return (T)field.GetValue(instance)!;
    }

    private sealed class FakeEventSubTransport : ITwitchEventSubTransport
    {
        public string? SessionId { get; set; } = "session-1";
        public int ConnectCallCount { get; private set; }
        public int ReconnectCallCount { get; private set; }
        public int DisconnectCallCount { get; private set; }
        public Queue<bool> ReconnectResults { get; } = [];

        public event Func<object, WebsocketConnectedArgs, Task> Connected = delegate { return Task.CompletedTask; };
        public event Func<object, System.EventArgs, Task> Disconnected = delegate { return Task.CompletedTask; };
        public event Func<object, System.EventArgs, Task> Reconnected = delegate { return Task.CompletedTask; };
        public event Func<object, ChannelChatMessageArgs, Task> ChatMessageReceived = delegate { return Task.CompletedTask; };
        public event Func<object, ChannelPointsCustomRewardRedemptionArgs, Task> RewardRedeemed = delegate { return Task.CompletedTask; };

        public Task ConnectAsync()
        {
            ConnectCallCount++;
            return Task.CompletedTask;
        }

        public Task<bool> ReconnectAsync()
        {
            ReconnectCallCount++;
            var result = ReconnectResults.Count > 0 && ReconnectResults.Dequeue();
            return Task.FromResult(result);
        }

        public Task DisconnectAsync()
        {
            DisconnectCallCount++;
            return Task.CompletedTask;
        }

        public Task RaiseConnectedAsync(WebsocketConnectedArgs args) => Connected(this, args);

        public Task RaiseDisconnectedAsync() => Disconnected(this, EventArgs.Empty);

        public Task RaiseReconnectedAsync() => Reconnected(this, EventArgs.Empty);

        public Task RaiseChatMessageAsync(ChannelChatMessageArgs args) => ChatMessageReceived(this, args);

        public Task RaiseRewardRedeemedAsync(ChannelPointsCustomRewardRedemptionArgs args) => RewardRedeemed(this, args);
    }

    private sealed class FakeEventSubSubscriptionClientFactory : ITwitchEventSubSubscriptionClientFactory
    {
        private readonly FakeEventSubSubscriptionClient _client;

        public FakeEventSubSubscriptionClientFactory(FakeEventSubSubscriptionClient? client = null)
        {
            _client = client ?? new FakeEventSubSubscriptionClient();
        }

        public List<string> CreatedWithTokens { get; } = [];
        public FakeEventSubSubscriptionClient Client => _client;

        public ITwitchEventSubSubscriptionClient Create(string accessToken)
        {
            CreatedWithTokens.Add(accessToken);
            return _client;
        }
    }

    private sealed class FakeEventSubSubscriptionClient : ITwitchEventSubSubscriptionClient
    {
        private readonly Dictionary<string, Queue<IReadOnlyList<string>>> _createResults = new(StringComparer.Ordinal);
        public List<(string Type, string Version, IReadOnlyDictionary<string, string> Conditions, string SessionId)> CreateCalls { get; } = [];
        public List<string> DeleteCalls { get; } = [];

        public void EnqueueCreateResult(string type, IReadOnlyList<string> result)
        {
            if (!_createResults.TryGetValue(type, out var queue))
            {
                queue = new Queue<IReadOnlyList<string>>();
                _createResults[type] = queue;
            }

            queue.Enqueue(result);
        }

        public Task<IReadOnlyList<string>> CreateSubscriptionAsync(string type, string version, IReadOnlyDictionary<string, string> conditions, string sessionId)
        {
            CreateCalls.Add((type, version, new Dictionary<string, string>(conditions, StringComparer.Ordinal), sessionId));
            if (!_createResults.TryGetValue(type, out var queue) || queue.Count == 0)
                return Task.FromResult((IReadOnlyList<string>)[]);

            return Task.FromResult(queue.Dequeue());
        }

        public Task DeleteSubscriptionAsync(string subscriptionId)
        {
            DeleteCalls.Add(subscriptionId);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAsyncDelay : IAsyncDelay
    {
        public List<TimeSpan> Delays { get; } = [];

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default)
        {
            Delays.Add(delay);
            return Task.CompletedTask;
        }
    }
}
