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
using TwitchLib.EventSub.Websockets.Core.EventArgs;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Channel;
using YouTubeMusicStreamer.Services.App;
using YouTubeMusicStreamer.Services.Twitch.Interfaces;

namespace YouTubeMusicStreamer.Services.Twitch.Implementations;

public sealed class TwitchEventSubService : ITwitchEventSubService
{
    private readonly ITwitchEventSubTransport _transport;
    private readonly ITwitchEventSubSubscriptionClientFactory _subscriptionClientFactory;
    private readonly ILogger<TwitchEventSubService> _logger;
    private readonly IAsyncDelay _asyncDelay;
    private string? _channelId;
    private string? _accessToken;
    private readonly HashSet<string> _ownedSubscriptionIds = [];

    public event EventHandler<ChannelChatMessageArgs>? OnChatMessage;
    public event EventHandler<ChannelPointsCustomRewardRedemptionArgs>? OnRewardRedeemed;

    public TwitchEventSubService(
        ITwitchEventSubTransport transport,
        ILogger<TwitchEventSubService> logger,
        ITwitchEventSubSubscriptionClientFactory subscriptionClientFactory,
        IAsyncDelay asyncDelay)
    {
        _transport = transport;
        _logger = logger;
        _subscriptionClientFactory = subscriptionClientFactory;
        _asyncDelay = asyncDelay;

        _transport.ChatMessageReceived += HandleChatMessage;
        _transport.RewardRedeemed += HandleRewardRedeemed;

        _transport.Connected += HandleConnected;
        _transport.Disconnected += HandleDisconnected;
        _transport.Reconnected += HandleReconnected;
    }

    /// <summary>
    /// Starts EventSub for a given broadcaster channel.
    /// </summary>
    public Task StartAsync(string channelId, string broadcasterAccessToken)
    {
        _channelId = channelId;
        _accessToken = broadcasterAccessToken;
        _logger.LogInformation("Connecting EventSub websocket for channel {ChannelId}", channelId);
        return _transport.ConnectAsync();
    }

    /// <summary>
    /// Stops EventSub and removes all subscriptions.
    /// </summary>
    public async Task StopAsync()
    {
        await _transport.DisconnectAsync();
        await DeleteOwnedAsync().ConfigureAwait(false);
        _logger.LogInformation("EventSub stopped");
    }

    private async Task HandleConnected(object sender, WebsocketConnectedArgs args)
    {
        _logger.LogInformation("Websocket connected (sessionId={SessionId})", _transport.SessionId);
        if (_channelId == null)
        {
            _logger.LogWarning("No channelId set, skipping subscription");
            return;
        }

        await DeleteOwnedAsync().ConfigureAwait(false);

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
            while (_retries < 5 && !await _transport.ReconnectAsync())
            {
                _reconnecting = true;
                _retries++;
                _logger.LogWarning("Reconnect attempt {Attempt} failed, retrying in 5 seconds", _retries);
                await _asyncDelay.DelayAsync(TimeSpan.FromSeconds(5));
            }

            if (_retries >= 5)
            {
                _logger.LogError("Failed to reconnect after 5 attempts, full restart");
                await StopAsync().ConfigureAwait(false);
                await StartAsync(_channelId!, _accessToken!).ConfigureAwait(false);
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
            await DeleteOwnedAsync().ConfigureAwait(false);
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
            var client = CreateSubscriptionClient();
            var subscriptionIds = await client.CreateSubscriptionAsync(type, "1", conditions, _transport.SessionId!);
            foreach (var subscriptionId in subscriptionIds)
                _ownedSubscriptionIds.Add(subscriptionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to subscribe to {Type}", type);
            throw;
        }

        _logger.LogInformation("Subscribed to {Type}", type);
    }


    private async Task DeleteOwnedAsync()
    {
        if (_ownedSubscriptionIds.Count == 0)
        {
            return;
        }

        foreach (var subscriptionId in _ownedSubscriptionIds.ToArray())
        {
            var client = CreateSubscriptionClient();
            await client.DeleteSubscriptionAsync(subscriptionId).ConfigureAwait(false);
            _ownedSubscriptionIds.Remove(subscriptionId);
            _logger.LogInformation("Deleted owned EventSub subscription {SubscriptionId}", subscriptionId);
        }
    }

    private ITwitchEventSubSubscriptionClient CreateSubscriptionClient()
    {
        if (string.IsNullOrWhiteSpace(_accessToken))
            throw new InvalidOperationException("EventSub broadcaster token is not available.");

        return _subscriptionClientFactory.Create(_accessToken);
    }
}
