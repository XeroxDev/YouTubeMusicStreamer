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

using TwitchLib.Api.Core.Enums;
using TwitchLib.EventSub.Websockets;
using TwitchLib.EventSub.Websockets.Core.EventArgs;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Channel;
using YouTubeMusicStreamer.Services.Twitch.Interfaces;

namespace YouTubeMusicStreamer.Services.Twitch.Implementations;

public sealed class TwitchEventSubTransport : ITwitchEventSubTransport
{
    private readonly EventSubWebsocketClient _websocketClient;

    public TwitchEventSubTransport(EventSubWebsocketClient websocketClient)
    {
        _websocketClient = websocketClient;
        _websocketClient.WebsocketConnected += HandleConnected;
        _websocketClient.WebsocketDisconnected += HandleDisconnected;
        _websocketClient.WebsocketReconnected += HandleReconnected;
        _websocketClient.ChannelChatMessage += HandleChatMessageReceived;
        _websocketClient.ChannelPointsCustomRewardRedemptionAdd += HandleRewardRedeemed;
    }

    public string? SessionId => _websocketClient.SessionId;

    public event Func<object, WebsocketConnectedArgs, Task> Connected = delegate { return Task.CompletedTask; };
    public event Func<object, System.EventArgs, Task> Disconnected = delegate { return Task.CompletedTask; };
    public event Func<object, System.EventArgs, Task> Reconnected = delegate { return Task.CompletedTask; };
    public event Func<object, ChannelChatMessageArgs, Task> ChatMessageReceived = delegate { return Task.CompletedTask; };
    public event Func<object, ChannelPointsCustomRewardRedemptionArgs, Task> RewardRedeemed = delegate { return Task.CompletedTask; };

    public Task ConnectAsync() => _websocketClient.ConnectAsync();

    public Task<bool> ReconnectAsync() => _websocketClient.ReconnectAsync();

    public Task DisconnectAsync() => _websocketClient.DisconnectAsync();

    private Task HandleConnected(object sender, WebsocketConnectedArgs args) => Connected(sender, args);

    private Task HandleDisconnected(object sender, System.EventArgs args) => Disconnected(sender, args);

    private Task HandleReconnected(object sender, System.EventArgs args) => Reconnected(sender, args);

    private Task HandleChatMessageReceived(object sender, ChannelChatMessageArgs args) => ChatMessageReceived(sender, args);

    private Task HandleRewardRedeemed(object sender, ChannelPointsCustomRewardRedemptionArgs args) => RewardRedeemed(sender, args);
}

public sealed class TwitchEventSubSubscriptionClientFactory(ITwitchApiFactory apiFactory) : ITwitchEventSubSubscriptionClientFactory
{
    public ITwitchEventSubSubscriptionClient Create(string accessToken) =>
        new TwitchEventSubSubscriptionClient(apiFactory.Create(accessToken));
}

internal sealed class TwitchEventSubSubscriptionClient(ITwitchApiClient api) : ITwitchEventSubSubscriptionClient
{
    public Task<IReadOnlyList<string>> CreateSubscriptionAsync(string type, string version, IReadOnlyDictionary<string, string> conditions, string sessionId) =>
        api.CreateEventSubSubscriptionAsync(type, version, conditions, sessionId);

    public Task DeleteSubscriptionAsync(string subscriptionId) => api.DeleteEventSubSubscriptionAsync(subscriptionId);
}
