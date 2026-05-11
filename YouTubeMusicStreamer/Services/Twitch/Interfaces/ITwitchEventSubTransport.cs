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

using TwitchLib.EventSub.Websockets.Core.EventArgs;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Channel;

namespace YouTubeMusicStreamer.Services.Twitch.Interfaces;

public interface ITwitchEventSubTransport
{
    string? SessionId { get; }
    event Func<object, WebsocketConnectedArgs, Task> Connected;
    event Func<object, System.EventArgs, Task> Disconnected;
    event Func<object, System.EventArgs, Task> Reconnected;
    event Func<object, ChannelChatMessageArgs, Task> ChatMessageReceived;
    event Func<object, ChannelPointsCustomRewardRedemptionArgs, Task> RewardRedeemed;
    Task ConnectAsync();
    Task<bool> ReconnectAsync();
    Task DisconnectAsync();
}

public interface ITwitchEventSubSubscriptionClient
{
    Task<IReadOnlyList<string>> CreateSubscriptionAsync(string type, string version, IReadOnlyDictionary<string, string> conditions, string sessionId);
    Task DeleteSubscriptionAsync(string subscriptionId);
}

public interface ITwitchEventSubSubscriptionClientFactory
{
    ITwitchEventSubSubscriptionClient Create(string accessToken);
}
