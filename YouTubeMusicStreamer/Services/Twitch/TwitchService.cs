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

using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using TwitchLib.Api.Helix.Models.ChannelPoints;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Channel;
using YouTubeMusicStreamer.Enums;
using YouTubeMusicStreamer.Services.App;
using YouTubeMusicStreamer.Services.Commands;
using YouTubeMusicStreamer.Services.Twitch.Implementations.EventArgs;
using YouTubeMusicStreamer.Services.Twitch.Interfaces;
using YouTubeMusicStreamer.Windows;

namespace YouTubeMusicStreamer.Services.Twitch;

public sealed partial class TwitchService : INotifyPropertyChanged, IDisposable
{
    // UI‐bound state
    private ConnectorState _state;
    private string? _username;
    private string? _profileImageUrl;
    private List<CustomReward> _rewards = [];

    public ConnectorState State
    {
        get => _state;
        private set => SetField(ref _state, value);
    }

    public string? Username
    {
        get => _username;
        private set => SetField(ref _username, value);
    }

    public string? ProfileImageUrl
    {
        get => _profileImageUrl;
        private set => SetField(ref _profileImageUrl, value);
    }

    public IReadOnlyList<CustomReward> Rewards
    {
        get => _rewards;
        private set => SetField(ref _rewards, value.ToList());
    }

    public event EventHandler<Exception>? OnError;
    public event PropertyChangedEventHandler? PropertyChanged;

    private readonly ITwitchTokenService _tokenService;
    private readonly ITwitchUserService _userService;
    private readonly ITwitchChatService _chatService;
    private readonly ITwitchEventSubService _eventSubService;
    private readonly CommandService _commandService;
    private readonly SettingsService _settingsService;
    private readonly ILogger<TwitchService> _logger;

    // private readonly ConcurrentDictionary<string, TaskCompletionSource<ChannelPointsCustomRewardRedemption>> _pendingRedemptions = new(concurrencyLevel: 1, capacity: 100);

    public TwitchService(
        SettingsService settingsService,
        ITwitchTokenService tokenService,
        ITwitchUserService userService,
        ITwitchChatService chatService,
        ITwitchEventSubService eventSubService,
        CommandService commandService,
        ILogger<TwitchService> logger)
    {
        _settingsService = settingsService;
        _tokenService = tokenService;
        _userService = userService;
        _chatService = chatService;
        _eventSubService = eventSubService;
        _commandService = commandService;
        _logger = logger;

        _tokenService.TokenValidated += OnTokenValidated;
        _userService.UserInitialized += OnUserInitialized;
        _eventSubService.OnChatMessage += OnChatMessage;
        _eventSubService.OnRewardRedeemed += OnRewardRedeemed;

        var saved = _settingsService.GetSensitiveSettings().TwitchAccessToken;
        State = string.IsNullOrWhiteSpace(saved)
            ? ConnectorState.LoggedOut
            : ConnectorState.Loading;

        if (!string.IsNullOrWhiteSpace(saved))
            _ = _tokenService.ValidateAsync(saved);
    }

    public Task StartOAuthFlowAsync(Action<string> onError)
    {
        _logger.LogInformation("Starting OAuth flow");
        State = ConnectorState.LoggingIn;

        Application.Current?.Dispatcher.Dispatch(() =>
        {
            var oauthWindow = new OAuthWindow(async void (response) =>
            {
                try
                {
                    var (error, state, token) = response;

                    if (error is not null)
                    {
                        State = state;
                        onError(error);
                        OnError?.Invoke(this, new Exception($"OAuth error: {error}"));
                        return;
                    }

                    if (string.IsNullOrEmpty(token))
                    {
                        onError("Received null or empty token during OAuth flow");
                        OnError?.Invoke(this, new Exception("Empty token from OAuth"));
                        return;
                    }

                    var valid = await _tokenService.ValidateAsync(token);
                    if (!valid)
                    {
                        onError("Received invalid token during OAuth flow");
                        OnError?.Invoke(this, new Exception("Invalid token from OAuth"));
                        return;
                    }

                    await _settingsService.SaveSensitiveSettingAsync(s => s.TwitchAccessToken = token);

                    _logger.LogInformation("OAuth successful, token stored");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during OAuth flow");
                    OnError?.Invoke(this, ex);
                    onError("Unexpected error during OAuth flow");
                }
            });

            Application.Current.OpenWindow(oauthWindow);
        });

        return Task.CompletedTask;
    }

    private CancellationTokenSource? _rewardRefreshCts;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public async Task RefreshRewardsAsync()
    {
        // Cancel any previous debounce request
        _rewardRefreshCts?.Cancel();
        _rewardRefreshCts?.Dispose();

        _rewardRefreshCts = new CancellationTokenSource();

        var token = _rewardRefreshCts.Token;

        try
        {
            await Task.Delay(100, token); // debounce delay (adjust as needed)
            await _refreshLock.WaitAsync(token);

            await _userService.RefreshRewardsAsync();
            Rewards = _userService.Rewards;
        }
        catch (TaskCanceledException)
        {
            // Silently swallow canceled attempts
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh rewards");
            OnError?.Invoke(this, ex);
        }
        finally
        {
            if (_refreshLock.CurrentCount == 0)
                _refreshLock.Release();
        }
    }


    // tear everything down
    public async Task LogoutAsync()
    {
        try
        {
            await _settingsService.SaveSensitiveSettingAsync(s => s.TwitchAccessToken = null);
            await _eventSubService.StopAsync();
            await _chatService.DisconnectAsync();

            Username = null;
            ProfileImageUrl = null;
            State = ConnectorState.LoggedOut;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            OnError?.Invoke(this, ex);
        }
    }

    private void OnTokenValidated(object? sender, TokenValidatedEventArgs args)
    {
        if (!args.IsValid)
        {
            _logger.LogWarning("Invalid Twitch token");
            State = ConnectorState.LoggedOut;
            _settingsService.SaveSensitiveSettingAsync(x => x.TwitchAccessToken = null).Wait();
            OnError?.Invoke(this, new Exception("Invalid Twitch Token"));
            return;
        }

        _settingsService.SaveSensitiveSettingAsync(s => s.TwitchAccessToken = args.Token)
            .ContinueWith(_ => _userService.InitializeAsync());
    }

    private async void OnUserInitialized(object? sender, UserInitializedEventArgs args)
    {
        try
        {
            Username = args.Username;
            ProfileImageUrl = args.ProfileImageUrl;
            State = ConnectorState.LoggedIn;

            // ready—start chat & EventSub
            await _chatService.ConnectAsync(args.Username, _settingsService.GetSensitiveSettings().TwitchAccessToken!);
            await _eventSubService.StartAsync(args.ChannelId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting up chat/EventSub");
            OnError?.Invoke(this, ex);
            State = ConnectorState.Error;
        }
    }

    private void OnChatMessage(object? sender, ChannelChatMessageArgs args)
    {
        var evt = args.Notification.Payload.Event;

        // redemption COA vs. pure chat
        var rewardId = evt.ChannelPointsCustomRewardId;
        if (!string.IsNullOrEmpty(rewardId) && string.IsNullOrEmpty(evt.Message.Text))
            return; // wait for the actual chat text

        _ = HandleChatAsync(evt);
    }

    private Task HandleChatAsync(ChannelChatMessage evt)
    {
        // var tcs = new TaskCompletionSource<CommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        _commandService.ProcessInput(evt/*, r => tcs.TrySetResult(r)*/);
        return Task.CompletedTask;
        /*var result = await tcs.Task.ConfigureAwait(false);

        var rewardId = evt.ChannelPointsCustomRewardId;
        if (string.IsNullOrWhiteSpace(rewardId))
            return;

        if (_pendingRedemptions.TryRemove(rewardId, out var redTcs))
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var redemption = await redTcs.Task.WaitAsync(cts.Token).ConfigureAwait(false);

                if (result == CommandResult.Success) ApproveRedemption(redemption);
                else RefundRedemption(redemption);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Timeout waiting for redemption {RewardId}", rewardId);
                OnError?.Invoke(this, new Exception($"Timeout waiting for redemption {rewardId}"));
            }
        }*/
    }

    private void OnRewardRedeemed(object? sender, ChannelPointsCustomRewardRedemptionArgs args)
    {
        var evt = args.Notification.Payload.Event;

        if (!string.IsNullOrEmpty(evt.UserInput))
        {
            /*var tcs = new TaskCompletionSource<ChannelPointsCustomRewardRedemption>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingRedemptions[evt.Reward.Id] = tcs;*/
            return;
        }

        // immediate handling if no user-input
        _commandService.ProcessInput(evt /*, result =>
        {
            if (result == CommandResult.Success) ApproveRedemption(evt);
            else RefundRedemption(evt);
        }*/);
    }

    // private void ApproveRedemption(ChannelPointsCustomRewardRedemption redemption)
    // {
    //     // For future. Can only be implemented, if the rewards are created by us
    //     // _logger.LogDebug("Approve redemption {Reward}/{Id}", redemption.Reward.Id, redemption.Id);
    // }
    //
    // private void RefundRedemption(ChannelPointsCustomRewardRedemption redemption)
    // {
    //     // For future. Can only be implemented, if the rewards are created by us
    //     // _logger.LogDebug("Refund redemption {Reward}/{Id}", redemption.Reward.Id, redemption.Id);
    // }

    #region INotifyPropertyChanged

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propName = null)
    {
        if (Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }

    #endregion

    public void Dispose()
    {
        _chatService.DisconnectAsync().Wait();
        _eventSubService.StopAsync().Wait();
    }
}