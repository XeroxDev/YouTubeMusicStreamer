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
using TwitchLib.Api;
using TwitchLib.Api.Core.Enums;
using TwitchLib.EventSub.Websockets;
using TwitchLib.EventSub.Websockets.Core.EventArgs;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Channel;
using YouTubeMusicStreamer.Services.Twitch.Interfaces;

namespace YouTubeMusicStreamer.Services.Twitch.Implementations;

public sealed class TwitchEventSubService : ITwitchEventSubService
{
    private readonly EventSubWebsocketClient _ws;
    private readonly TwitchAPI _api;
    private readonly ILogger<TwitchEventSubService> _logger;
    private string? _channelId;

    public event EventHandler<ChannelChatMessageArgs>? OnChatMessage;
    public event EventHandler<ChannelPointsCustomRewardRedemptionArgs>? OnRewardRedeemed;

    public TwitchEventSubService(
        EventSubWebsocketClient wsClient,
        ILogger<TwitchEventSubService> logger,
        TwitchAPI api)
    {
        _ws = wsClient;
        _logger = logger;
        _api = api;

        _ws.ChannelChatMessage += HandleChatMessage;
        _ws.ChannelPointsCustomRewardRedemptionAdd += HandleRewardRedeemed;

        _ws.WebsocketConnected += HandleConnected;
        _ws.WebsocketDisconnected += HandleDisconnected;
        _ws.WebsocketReconnected += HandleReconnected;
    }

    /// <summary>
    /// Starts EventSub for a given broadcaster channel.
    /// </summary>
    public Task StartAsync(string channelId)
    {
        _channelId = channelId;
        _logger.LogInformation("Connecting EventSub websocket for channel {ChannelId}", channelId);
        return _ws.ConnectAsync();
    }

    /// <summary>
    /// Stops EventSub and removes all subscriptions.
    /// </summary>
    public async Task StopAsync()
    {
        await _ws.DisconnectAsync();
        await DeleteAllAsync().ConfigureAwait(false);
        _logger.LogInformation("EventSub stopped");
    }

    private async Task HandleConnected(object sender, WebsocketConnectedArgs args)
    {
        _logger.LogInformation("Websocket connected (sessionId={SessionId})", _ws.SessionId);
        if (_channelId == null)
        {
            _logger.LogWarning("No channelId set, skipping subscription");
            return;
        }

        // Ensure old subscriptions cleared
        await DeleteAllAsync().ConfigureAwait(false);

        // Subscribe to chat and reward events
        await Subscribe(_channelId, "channel.chat.message").ConfigureAwait(false);
        await Subscribe(_channelId, "channel.channel_points_custom_reward_redemption.add").ConfigureAwait(false);
    }

    private bool _reconnecting;
    private int _retries;

    private async Task HandleDisconnected(object sender, System.EventArgs eventArgs)
    {
        if (_reconnecting) return;

        try
        {
            _logger.LogWarning("Websocket disconnected, trying to reconnect");
            while (!await _ws.ReconnectAsync() && _retries < 5)
            {
                _reconnecting = true;
                _retries++;
                _logger.LogWarning("Reconnect attempt {Attempt} failed, retrying in 5 seconds", _retries);
                await Task.Delay(TimeSpan.FromSeconds(5));
            }

            if (_retries >= 5)
            {
                _logger.LogError("Failed to reconnect after 5 attempts, full restart");
                await StopAsync().ConfigureAwait(false);
                await StartAsync(_channelId!).ConfigureAwait(false);
            }
        }
        finally
        {
            _reconnecting = false;
            _retries = 0;
        }
    }

    private async Task HandleReconnected(object sender, System.EventArgs eventArgs)
    {
        _logger.LogInformation("Websocket reconnected, re-subscribing");
        if (_channelId != null)
        {
            await Subscribe(_channelId, "channel.chat.message").ConfigureAwait(false);
            await Subscribe(_channelId, "channel.channel_points_custom_reward_redemption.add").ConfigureAwait(false);
        }
    }

    private Task HandleChatMessage(object sender, ChannelChatMessageArgs e)
    {
        OnChatMessage?.Invoke(this, e);
        return Task.CompletedTask;
    }

    private Task HandleRewardRedeemed(object sender, ChannelPointsCustomRewardRedemptionArgs e)
    {
        OnRewardRedeemed?.Invoke(this, e);
        return Task.CompletedTask;
    }

    private async Task Subscribe(string channelId, string type)
    {
        var conditions = type switch
        {
            "channel.chat.message" => new Dictionary<string, string>
            {
                { "broadcaster_user_id", channelId },
                { "user_id", channelId }
            },
            _ => new Dictionary<string, string>
            {
                { "broadcaster_user_id", channelId }
            }
        };

        _logger.LogInformation("Subscribing to {Type} for channel {ChannelId}", type, channelId);

        try
        {
            await _api.Helix.EventSub.CreateEventSubSubscriptionAsync(
                type,
                "1",
                conditions,
                EventSubTransportMethod.Websocket,
                _ws.SessionId
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to subscribe to {Type}", type);
            throw;
        }

        _logger.LogInformation("Subscribed to {Type}", type);
    }


    private async Task DeleteAllAsync()
    {
        var subs = await _api.Helix.EventSub
            .GetEventSubSubscriptionsAsync()
            .ConfigureAwait(false);
        if (subs?.Subscriptions == null || subs.Subscriptions.Length == 0)
        {
            _logger.LogInformation("No existing subscriptions to delete");
            return;
        }

        foreach (var s in subs.Subscriptions)
        {
            await _api.Helix.EventSub
                .DeleteEventSubSubscriptionAsync(s.Id)
                .ConfigureAwait(false);
            _logger.LogInformation("Deleted subscription {Type}", s.Type);
        }
    }
}