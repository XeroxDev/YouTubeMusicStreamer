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

using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;

namespace YouTubeMusicStreamer.Services.Twitch.Interfaces;

public interface ITwitchChatClient
{
    event EventHandler<TwitchChatConnectedEventArgs> Connected;
    event EventHandler<TwitchChatJoinedChannelEventArgs> JoinedChannel;

    bool IsConnected { get; }

    void Initialize(string username, string token, string channelLogin);
    void Connect();
    void Disconnect();
    void JoinChannel(string channelName);
    void SendReply(string channelName, string messageId, string message);
    void SendMessage(string channelName, string message);
}

public interface ITwitchChatClientFactory
{
    ITwitchChatClient Create();
}

public sealed record TwitchChatConnectedEventArgs;

public sealed record TwitchChatJoinedChannelEventArgs(string Channel);

public interface ITwitchChatService
{
    Task ConnectAsync(string username, string accessToken, string channelLogin);
    Task DisconnectAsync();
    void SendMessage(ChannelChatMessage senderMessage, string message, bool asReply = true);
}
