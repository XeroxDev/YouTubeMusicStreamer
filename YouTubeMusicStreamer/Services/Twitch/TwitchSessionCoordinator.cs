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

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Channel;
using YouTubeMusicStreamer.Services.App;
using YouTubeMusicStreamer.Services.App.Diagnostics;
using YouTubeMusicStreamer.Services.App.Persistence;
using YouTubeMusicStreamer.Services.Commands;
using YouTubeMusicStreamer.Services.Twitch.Implementations;
using YouTubeMusicStreamer.Services.Twitch.Interfaces;

namespace YouTubeMusicStreamer.Services.Twitch;

public sealed class TwitchSessionCoordinator : ITwitchSessionCoordinator, IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly ITwitchAuthService _authService;
    private readonly ITwitchTokenService _tokenService;
    private readonly ITwitchUserService _userService;
    private readonly ITwitchRewardService _rewardService;
    private readonly ITwitchChatService _chatService;
    private readonly ITwitchEventSubService _eventSubService;
    private readonly CommandService _commandService;
    private readonly ILogger<TwitchSessionCoordinator> _logger;
    private readonly IAppDiagnosticsService _diagnosticsService;
    private readonly SemaphoreSlim _sessionLock = new(1, 1);
    private readonly ConcurrentDictionary<string, PendingRewardRedemption> _pendingRewardRedemptions = new(StringComparer.Ordinal);

    private TwitchIdentitySnapshot? _broadcasterIdentity;
    private TwitchIdentitySnapshot? _botIdentity;
    private List<TwitchRewardSnapshot> _rewards = [];
    private bool _broadcasterEventSubConnected;
    private bool _chatConnected;
    private TwitchAccountRole _effectiveChatRole = TwitchAccountRole.Broadcaster;
    private CancellationTokenSource? _tokenValidationLoopCancellation;
    private Task? _tokenValidationLoopTask;
    private bool _disposed;
    private static readonly TimeSpan RewardCorrelationTimeout = TimeSpan.FromSeconds(10);

    public TwitchSessionCoordinator(
        SettingsService settingsService,
        ITwitchAuthService authService,
        ITwitchTokenService tokenService,
        ITwitchUserService userService,
        ITwitchRewardService rewardService,
        ITwitchChatService chatService,
        ITwitchEventSubService eventSubService,
        CommandService commandService,
        IAppDiagnosticsService diagnosticsService,
        ILogger<TwitchSessionCoordinator> logger)
    {
        _settingsService = settingsService;
        _authService = authService;
        _tokenService = tokenService;
        _userService = userService;
        _rewardService = rewardService;
        _chatService = chatService;
        _eventSubService = eventSubService;
        _commandService = commandService;
        _diagnosticsService = diagnosticsService;
        _logger = logger;

        CurrentState = CreateState(TwitchSessionStatus.LoggedOut);

        _eventSubService.OnChatMessage += OnChatMessage;
        _eventSubService.OnRewardRedeemed += OnRewardRedeemed;
    }

    public TwitchSessionState CurrentState { get; private set; }

    public event EventHandler<TwitchSessionState> StateChanged = delegate { };
    public event EventHandler<TwitchSessionFailure> SessionFaulted = delegate { };

    public async Task InitializeIfNeededAsync()
    {
        var sensitive = await _settingsService.GetSensitiveSettingsAsync();
        var broadcasterToken = sensitive.TwitchBroadcasterAccessToken ?? sensitive.TwitchAccessToken;
        if (string.IsNullOrWhiteSpace(broadcasterToken))
        {
            UpdateState(CreateState(TwitchSessionStatus.LoggedOut));
            return;
        }

        await ConnectBroadcasterSessionAsync(
            broadcasterToken,
            sensitive.TwitchBotAccessToken,
            persistBroadcasterToken: false,
            preserveExistingBotToken: true);
    }

    public async Task<TwitchSessionFailure?> StartBroadcasterAuthAsync(CancellationToken cancellationToken = default)
    {
        var authResult = await _authService.StartAsync(TwitchAccountRole.Broadcaster, cancellationToken);
        if (authResult.Status != TwitchAuthStatus.Success)
        {
            var failure = MapAuthFailure(authResult);
            UpdateState(CreateState(
                failure.Kind == TwitchFailureKind.AuthCancelled ? TwitchSessionStatus.LoggedOut : TwitchSessionStatus.Error,
                failure));
            SessionFaulted(this, failure);
            return failure;
        }

        var currentSensitive = await _settingsService.GetSensitiveSettingsAsync();
        return await ConnectBroadcasterSessionAsync(
            authResult.AccessToken!,
            currentSensitive.TwitchBotAccessToken,
            persistBroadcasterToken: true,
            preserveExistingBotToken: true);
    }

    public async Task<TwitchSessionFailure?> StartBotAuthAsync(CancellationToken cancellationToken = default)
    {
        if (_broadcasterIdentity is null)
        {
            var failure = new TwitchSessionFailure(TwitchFailureKind.AuthValidation, "A broadcaster account must be connected before adding a bot account.");
            UpdateState(CreateState(TwitchSessionStatus.AuthRequired, failure));
            SessionFaulted(this, failure);
            return failure;
        }

        var authResult = await _authService.StartAsync(TwitchAccountRole.Bot, cancellationToken);
        if (authResult.Status != TwitchAuthStatus.Success)
        {
            var failure = MapAuthFailure(authResult);
            UpdateState(CreateState(
                failure.Kind == TwitchFailureKind.AuthCancelled ? CurrentState.Status : TwitchSessionStatus.Degraded,
                failure));
            SessionFaulted(this, failure);
            return failure;
        }

        await _sessionLock.WaitAsync(cancellationToken);
        try
        {
            await DisconnectChatAsync();
            var botFailure = await ConnectBotChatAsync(authResult.AccessToken!, persistToken: true);
            if (botFailure is null)
            {
                StartTokenValidationLoop();
                UpdateState(CreateState(TwitchSessionStatus.Ready));
                return null;
            }

            await ConnectChatAsync(_broadcasterIdentity, await GetBroadcasterTokenAsync(), TwitchAccountRole.Broadcaster);
            UpdateState(CreateState(TwitchSessionStatus.Degraded, botFailure));
            SessionFaulted(this, botFailure);
            return botFailure;
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    public async Task DisconnectBotAsync()
    {
        await _sessionLock.WaitAsync();
        try
        {
            if (_broadcasterIdentity is null)
            {
                await ClearBotAsync();
                UpdateState(CreateState(TwitchSessionStatus.LoggedOut));
                return;
            }

            await DisconnectChatAsync();
            await ClearBotAsync();
            await ConnectChatAsync(_broadcasterIdentity, await GetBroadcasterTokenAsync(), TwitchAccountRole.Broadcaster);
            StartTokenValidationLoop();
            UpdateState(CreateState(TwitchSessionStatus.Ready));
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    public async Task LogoutAsync()
    {
        await _sessionLock.WaitAsync();
        try
        {
            UpdateState(CreateState(TwitchSessionStatus.SwitchingAccount));
            await DisconnectSessionAsync();
            await _settingsService.SaveSensitiveSettingAsync(s =>
            {
                s.TwitchAccessToken = null;
                s.TwitchBroadcasterAccessToken = null;
                s.TwitchBotAccessToken = null;
            });
            await PersistAccountMetadataAsync(null, null);
            UpdateState(CreateState(TwitchSessionStatus.LoggedOut));
        }
        catch (Exception ex)
        {
            var failure = new TwitchSessionFailure(TwitchFailureKind.AccountReset, "Failed to log out from Twitch cleanly.", ex);
            UpdateState(CreateState(TwitchSessionStatus.Error, failure));
            SessionFaulted(this, failure);
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    public async Task RefreshRewardsAsync()
    {
        if (_broadcasterIdentity is null)
        {
            _rewards = [];
            UpdateState(CreateState(CurrentState.Status, CurrentState.LastFailure));
            return;
        }

        try
        {
            var broadcasterToken = await GetBroadcasterTokenAsync();
            _rewards = (await _userService.GetRewardsAsync(broadcasterToken, _broadcasterIdentity.UserId)).ToList();
            await SyncManagedRewardBindingsAsync();
            UpdateState(CreateState(CurrentState.Status, CurrentState.LastFailure?.Kind == TwitchFailureKind.RewardLoad ? null : CurrentState.LastFailure));
        }
        catch (Exception ex)
        {
            var failure = new TwitchSessionFailure(TwitchFailureKind.RewardLoad, "Failed to refresh Twitch rewards.", ex);
            _rewards = [];
            UpdateState(CreateState(TwitchSessionStatus.Degraded, failure));
            SessionFaulted(this, failure);
        }
    }

    public async Task<ManagedRewardSyncResult> RefreshManagedRewardBindingAsync(
        CommandRewardBindingSnapshot binding,
        bool commandRequiresInput,
        CancellationToken cancellationToken = default)
    {
        var clone = binding.Clone();
        if (_broadcasterIdentity is null)
            throw new InvalidOperationException("A broadcaster account must be connected before refreshing a managed reward.");

        clone.BroadcasterAccountId = _broadcasterIdentity.UserId;

        if (string.IsNullOrWhiteSpace(clone.RewardId))
        {
            clone.ManagedRewardCompatibilityState = ManagedRewardCompatibilityState.Missing;
            clone.ManagedRewardCompatibilityMessage = "Managed reward has not been created on Twitch yet.";
            return new ManagedRewardSyncResult(clone, false);
        }

        var reward = await _rewardService.GetRewardByIdAsync(
            await GetBroadcasterTokenAsync(),
            _broadcasterIdentity.UserId,
            clone.RewardId,
            cancellationToken);

        return BuildManagedRewardSyncResult(clone, reward, commandRequiresInput);
    }

    public async Task<ManagedRewardSyncResult> CreateManagedRewardAsync(
        CommandRewardBindingSnapshot binding,
        bool commandRequiresInput,
        CancellationToken cancellationToken = default)
    {
        var clone = binding.Clone();
        if (_broadcasterIdentity is null)
            throw new InvalidOperationException("A broadcaster account must be connected before creating a managed reward.");

        var reward = await _rewardService.CreateManagedRewardAsync(
            await GetBroadcasterTokenAsync(),
            _broadcasterIdentity.UserId,
            CreateManagedRewardDefinition(clone, commandRequiresInput),
            cancellationToken);

        await RefreshRewardsAsync();
        return BuildManagedRewardSyncResult(clone, reward, commandRequiresInput);
    }

    public async Task<ManagedRewardSyncResult> UpdateManagedRewardAsync(
        CommandRewardBindingSnapshot binding,
        bool commandRequiresInput,
        CancellationToken cancellationToken = default)
    {
        var clone = binding.Clone();
        if (_broadcasterIdentity is null)
            throw new InvalidOperationException("A broadcaster account must be connected before updating a managed reward.");

        if (string.IsNullOrWhiteSpace(clone.RewardId))
            return await CreateManagedRewardAsync(clone, commandRequiresInput, cancellationToken);

        var reward = await _rewardService.UpdateManagedRewardAsync(
            await GetBroadcasterTokenAsync(),
            _broadcasterIdentity.UserId,
            clone.RewardId,
            CreateManagedRewardDefinition(clone, commandRequiresInput),
            cancellationToken);

        await RefreshRewardsAsync();
        return BuildManagedRewardSyncResult(clone, reward, commandRequiresInput);
    }

    private async Task<TwitchSessionFailure?> ConnectBroadcasterSessionAsync(
        string broadcasterToken,
        string? botToken,
        bool persistBroadcasterToken,
        bool preserveExistingBotToken)
    {
        await _sessionLock.WaitAsync();
        try
        {
            UpdateState(CreateState(TwitchSessionStatus.ValidatingBroadcaster));

            await DisconnectSessionAsync();

            var valid = await _tokenService.ValidateAsync(broadcasterToken);
            if (!valid)
            {
                await _settingsService.SaveSensitiveSettingAsync(s =>
                {
                    s.TwitchAccessToken = null;
                    s.TwitchBroadcasterAccessToken = null;
                    if (!preserveExistingBotToken)
                        s.TwitchBotAccessToken = null;
                });

                var invalidFailure = new TwitchSessionFailure(TwitchFailureKind.AuthValidation, "Invalid Twitch broadcaster token.");
                UpdateState(CreateState(TwitchSessionStatus.LoggedOut, invalidFailure));
                SessionFaulted(this, invalidFailure);
                return invalidFailure;
            }

            if (persistBroadcasterToken)
            {
                await _settingsService.SaveSensitiveSettingAsync(s =>
                {
                    s.TwitchAccessToken = null;
                    s.TwitchBroadcasterAccessToken = broadcasterToken;
                });
            }

            var identity = await _userService.GetCurrentUserAsync(broadcasterToken);
            if (identity is null)
            {
                var identityFailure = new TwitchSessionFailure(TwitchFailureKind.IdentityBootstrap, "Failed to load Twitch broadcaster identity.");
                UpdateState(CreateState(TwitchSessionStatus.Error, identityFailure));
                SessionFaulted(this, identityFailure);
                return identityFailure;
            }

            _broadcasterIdentity = new TwitchIdentitySnapshot(identity.UserId, identity.Login, identity.DisplayName, identity.ProfileImageUrl);
            UpdateState(CreateState(TwitchSessionStatus.BroadcasterReady));

            TwitchSessionFailure? rewardFailure = null;
            try
            {
                _rewards = (await _userService.GetRewardsAsync(broadcasterToken, identity.UserId)).ToList();
                await SyncManagedRewardBindingsAsync();
            }
            catch (Exception ex)
            {
                rewardFailure = new TwitchSessionFailure(TwitchFailureKind.RewardLoad, "Failed to load Twitch rewards.", ex);
                _rewards = [];
                UpdateState(CreateState(TwitchSessionStatus.Degraded, rewardFailure));
                SessionFaulted(this, rewardFailure);
            }

            UpdateState(CreateState(TwitchSessionStatus.ConnectingEventSub));
            await _eventSubService.StartAsync(identity.UserId, broadcasterToken);
            _broadcasterEventSubConnected = true;

            TwitchSessionFailure? botFailure = null;
            if (!string.IsNullOrWhiteSpace(botToken))
            {
                botFailure = await ConnectBotChatAsync(botToken, persistToken: false);
            }

            if (_botIdentity is null)
            {
                UpdateState(CreateState(TwitchSessionStatus.ConnectingChat, botFailure));
                await ConnectChatAsync(_broadcasterIdentity, broadcasterToken, TwitchAccountRole.Broadcaster);
            }

            await PersistAccountMetadataAsync(
                new TwitchAccountMetadataSnapshot(identity.UserId, identity.Login, identity.DisplayName, identity.ProfileImageUrl),
                _botIdentity is null ? null : new TwitchAccountMetadataSnapshot(_botIdentity.UserId, _botIdentity.Login, _botIdentity.DisplayName, _botIdentity.ProfileImageUrl));

            StartTokenValidationLoop();

            var finalFailure = botFailure ?? rewardFailure;
            UpdateState(CreateState(finalFailure is null ? TwitchSessionStatus.Ready : TwitchSessionStatus.Degraded, finalFailure));
            return finalFailure;
        }
        catch (Exception ex)
        {
            var failure = ClassifyConnectionFailure(ex);
            UpdateState(CreateState(TwitchSessionStatus.Error, failure));
            SessionFaulted(this, failure);
            return failure;
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    private static TwitchManagedRewardDefinition CreateManagedRewardDefinition(CommandRewardBindingSnapshot binding, bool commandRequiresInput) =>
        new(
            string.IsNullOrWhiteSpace(binding.ManagedRewardName) ? "New command reward" : binding.ManagedRewardName,
            binding.ManagedRewardPrompt,
            binding.ManagedRewardCost == 0 ? 1000u : binding.ManagedRewardCost,
            commandRequiresInput && binding.ManagedRewardRequiresUserInput);

    private async Task SyncManagedRewardBindingsAsync()
    {
        foreach (var descriptor in _commandService.GetAllCommandsInfo())
        {
            if (descriptor.CommandConfiguration.RewardTriggerMode != RewardTriggerMode.AppManaged ||
                descriptor.CommandConfiguration.RewardBinding is null)
            {
                continue;
            }

            var currentBinding = descriptor.CommandConfiguration.RewardBinding;
            var reward = _rewards.FirstOrDefault(r => string.Equals(r.Id, currentBinding.RewardId, StringComparison.Ordinal));
            var syncResult = BuildManagedRewardSyncResult(
                currentBinding,
                reward is null ? null : MapReward(reward),
                descriptor.RequiresInput);

            if (!AreEquivalent(currentBinding, syncResult.Binding))
            {
                var updatedConfiguration = descriptor.CommandConfiguration.Clone();
                updatedConfiguration.RewardBinding = syncResult.Binding;
                await _commandService.SaveCommandConfigurationAsync(descriptor.Name, updatedConfiguration);
            }
        }
    }

    private static ManagedRewardSyncResult BuildManagedRewardSyncResult(
        CommandRewardBindingSnapshot binding,
        TwitchManagedReward? reward,
        bool commandRequiresInput)
    {
        var clone = binding.Clone();
        if (reward is null)
        {
            clone.ManagedRewardCompatibilityState = ManagedRewardCompatibilityState.Missing;
            clone.ManagedRewardCompatibilityMessage = "Managed reward is missing on Twitch.";
            return new ManagedRewardSyncResult(clone, false);
        }

        clone.RewardId = reward.Id;
        clone.ManagedRewardName = reward.Title;
        clone.ManagedRewardPrompt = reward.Prompt;
        clone.ManagedRewardCost = reward.Cost;
        clone.ManagedRewardRequiresUserInput = reward.RequiresUserInput;

        if (commandRequiresInput && !reward.RequiresUserInput)
        {
            clone.ManagedRewardCompatibilityState = ManagedRewardCompatibilityState.Incompatible;
            clone.ManagedRewardCompatibilityMessage = "This command requires reward input, but the linked Twitch reward does not require user input.";
        }
        else
        {
            clone.ManagedRewardCompatibilityState = ManagedRewardCompatibilityState.Compatible;
            clone.ManagedRewardCompatibilityMessage = null;
        }

        return new ManagedRewardSyncResult(clone, true);
    }

    private static TwitchManagedReward MapReward(TwitchRewardSnapshot reward) =>
        new(
            reward.Id,
            reward.Title,
            reward.Prompt,
            reward.Cost,
            reward.RequiresUserInput);

    private static bool AreEquivalent(CommandRewardBindingSnapshot left, CommandRewardBindingSnapshot right) =>
        string.Equals(left.RewardId, right.RewardId, StringComparison.Ordinal) &&
        string.Equals(left.BroadcasterAccountId, right.BroadcasterAccountId, StringComparison.Ordinal) &&
        string.Equals(left.ManagedRewardName, right.ManagedRewardName, StringComparison.Ordinal) &&
        string.Equals(left.ManagedRewardPrompt, right.ManagedRewardPrompt, StringComparison.Ordinal) &&
        left.ManagedRewardCost == right.ManagedRewardCost &&
        left.ManagedRewardRequiresUserInput == right.ManagedRewardRequiresUserInput &&
        left.ManagedRewardCompatibilityState == right.ManagedRewardCompatibilityState &&
        string.Equals(left.ManagedRewardCompatibilityMessage, right.ManagedRewardCompatibilityMessage, StringComparison.Ordinal);

    private async Task<TwitchSessionFailure?> ConnectBotChatAsync(string token, bool persistToken)
    {
        if (_broadcasterIdentity is null)
            return new TwitchSessionFailure(TwitchFailureKind.AuthValidation, "A broadcaster account must be ready before a bot can connect.");

        UpdateState(CreateState(TwitchSessionStatus.ValidatingBot));

        var valid = await _tokenService.ValidateAsync(token);
        if (!valid)
        {
            await _settingsService.SaveSensitiveSettingAsync(s => s.TwitchBotAccessToken = null);
            _botIdentity = null;
            return new TwitchSessionFailure(TwitchFailureKind.AuthValidation, "Invalid Twitch bot token.");
        }

        var identity = await _userService.GetCurrentUserAsync(token);
        if (identity is null)
        {
            _botIdentity = null;
            return new TwitchSessionFailure(TwitchFailureKind.IdentityBootstrap, "Failed to load Twitch bot identity.");
        }

        if (persistToken)
        {
            await _settingsService.SaveSensitiveSettingAsync(s => s.TwitchBotAccessToken = token);
        }

        _botIdentity = new TwitchIdentitySnapshot(identity.UserId, identity.Login, identity.DisplayName, identity.ProfileImageUrl);
        await ConnectChatAsync(_botIdentity, token, TwitchAccountRole.Bot);
        return null;
    }

    private async Task ConnectChatAsync(TwitchIdentitySnapshot identity, string token, TwitchAccountRole role)
    {
        await _chatService.ConnectAsync(identity.Login, token, _broadcasterIdentity?.Login ?? identity.Login);
        _chatConnected = true;
        _effectiveChatRole = role;
    }

    private async Task DisconnectSessionAsync()
    {
        StopTokenValidationLoop();
        await DisconnectChatAsync();
        await DisconnectEventSubAsync();
        _broadcasterIdentity = null;
        _botIdentity = null;
        _rewards = [];
        _effectiveChatRole = TwitchAccountRole.Broadcaster;
    }

    private async Task DisconnectChatAsync()
    {
        await _chatService.DisconnectAsync();
        _chatConnected = false;
    }

    private async Task DisconnectEventSubAsync()
    {
        await _eventSubService.StopAsync();
        _broadcasterEventSubConnected = false;
    }

    private async Task ClearBotAsync()
    {
        _botIdentity = null;
        await _settingsService.SaveSensitiveSettingAsync(s => s.TwitchBotAccessToken = null);
        await PersistAccountMetadataAsync(
            _broadcasterIdentity is null ? null : new TwitchAccountMetadataSnapshot(_broadcasterIdentity.UserId, _broadcasterIdentity.Login, _broadcasterIdentity.DisplayName, _broadcasterIdentity.ProfileImageUrl),
            null);
    }

    private async Task PersistAccountMetadataAsync(TwitchAccountMetadataSnapshot? broadcaster, TwitchAccountMetadataSnapshot? bot)
    {
        var twitchSettings = _settingsService.GetTwitchSettings();
        await _settingsService.SaveTwitchSettingsAsync(twitchSettings with
        {
            BroadcasterAccount = broadcaster,
            BotAccount = bot
        });
    }

    private async Task<string> GetBroadcasterTokenAsync()
    {
        var sensitive = await _settingsService.GetSensitiveSettingsAsync();
        var token = sensitive.TwitchBroadcasterAccessToken ?? sensitive.TwitchAccessToken;
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("Broadcaster token is not available.");
        return token;
    }

    private void StartTokenValidationLoop()
    {
        StopTokenValidationLoop();
        if (_broadcasterIdentity is null)
            return;

        _tokenValidationLoopCancellation = new CancellationTokenSource();
        _tokenValidationLoopTask = Task.Run(() => RunTokenValidationLoopAsync(_tokenValidationLoopCancellation.Token));
    }

    private void StopTokenValidationLoop()
    {
        if (_tokenValidationLoopCancellation is null)
            return;

        _tokenValidationLoopCancellation.Cancel();
        _tokenValidationLoopCancellation.Dispose();
        _tokenValidationLoopCancellation = null;
        _tokenValidationLoopTask = null;
    }

    private async Task RunTokenValidationLoopAsync(CancellationToken cancellationToken)
    {
        var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await ValidateConfiguredTokensAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // normal shutdown
        }
        finally
        {
            timer.Dispose();
        }
    }

    private async Task ValidateConfiguredTokensAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await _sessionLock.WaitAsync(cancellationToken);
        try
        {
            var sensitive = await _settingsService.GetSensitiveSettingsAsync();
            var broadcasterToken = sensitive.TwitchBroadcasterAccessToken ?? sensitive.TwitchAccessToken;
            if (!string.IsNullOrWhiteSpace(broadcasterToken) && !await _tokenService.ValidateAsync(broadcasterToken))
            {
                var failure = new TwitchSessionFailure(TwitchFailureKind.AuthValidation, "Saved Twitch broadcaster token is no longer valid.");
                await DisconnectSessionAsync();
                await _settingsService.SaveSensitiveSettingAsync(s =>
                {
                    s.TwitchAccessToken = null;
                    s.TwitchBroadcasterAccessToken = null;
                    s.TwitchBotAccessToken = null;
                });
                await PersistAccountMetadataAsync(null, null);
                UpdateState(CreateState(TwitchSessionStatus.LoggedOut, failure));
                SessionFaulted(this, failure);
                return;
            }

            if (!string.IsNullOrWhiteSpace(sensitive.TwitchBotAccessToken) && !await _tokenService.ValidateAsync(sensitive.TwitchBotAccessToken))
            {
                await DisconnectChatAsync();
                await ClearBotAsync();
                if (_broadcasterIdentity is not null)
                {
                    await ConnectChatAsync(_broadcasterIdentity, broadcasterToken!, TwitchAccountRole.Broadcaster);
                }

                var failure = new TwitchSessionFailure(TwitchFailureKind.AuthValidation, "Saved Twitch bot token is no longer valid.");
                UpdateState(CreateState(TwitchSessionStatus.Degraded, failure));
                SessionFaulted(this, failure);
            }
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    private TwitchSessionFailure MapAuthFailure(TwitchAuthResult authResult)
    {
        return authResult.Status switch
        {
            TwitchAuthStatus.Cancelled => new TwitchSessionFailure(TwitchFailureKind.AuthCancelled, authResult.ErrorMessage ?? "Twitch authentication was cancelled."),
            TwitchAuthStatus.Failed when (authResult.ErrorMessage?.Contains("bind", StringComparison.OrdinalIgnoreCase) == true)
                => new TwitchSessionFailure(TwitchFailureKind.CallbackListenerBind, authResult.ErrorMessage),
            _ => new TwitchSessionFailure(TwitchFailureKind.Unknown, authResult.ErrorMessage ?? "Unexpected Twitch authentication failure.")
        };
    }

    private TwitchSessionFailure ClassifyConnectionFailure(Exception ex)
    {
        if (CurrentState.Status == TwitchSessionStatus.ConnectingChat)
            return new TwitchSessionFailure(TwitchFailureKind.ChatTransport, "Failed to connect Twitch chat.", ex);
        if (CurrentState.Status == TwitchSessionStatus.ConnectingEventSub)
            return new TwitchSessionFailure(TwitchFailureKind.EventSubTransport, "Failed to connect Twitch EventSub.", ex);
        if (CurrentState.Status is TwitchSessionStatus.ValidatingBroadcaster or TwitchSessionStatus.ValidatingBot)
            return new TwitchSessionFailure(TwitchFailureKind.AuthValidation, "Failed to validate Twitch token.", ex);
        return new TwitchSessionFailure(TwitchFailureKind.Unknown, ex.Message, ex);
    }

    private TwitchSessionState CreateState(TwitchSessionStatus status, TwitchSessionFailure? failure = null)
    {
        var broadcaster = new TwitchAccountSession(
            TwitchAccountRole.Broadcaster,
            _broadcasterIdentity is not null,
            _chatConnected && _effectiveChatRole == TwitchAccountRole.Broadcaster,
            _broadcasterEventSubConnected,
            _broadcasterIdentity);

        var bot = _botIdentity is null
            ? null
            : new TwitchAccountSession(
                TwitchAccountRole.Bot,
                true,
                _chatConnected && _effectiveChatRole == TwitchAccountRole.Bot,
                false,
                _botIdentity);

        return new TwitchSessionState(
            status,
            broadcaster,
            bot,
            _effectiveChatRole,
            _rewards.ToList(),
            failure);
    }

    private void UpdateState(TwitchSessionState state)
    {
        CurrentState = state;
        StateChanged(this, CurrentState);
    }

    private async void OnChatMessage(object? sender, ChannelChatMessageArgs args)
    {
        try
        {
            var evt = args.Notification.Payload.Event;
            var rewardId = evt.ChannelPointsCustomRewardId;
            if (!string.IsNullOrEmpty(rewardId) && string.IsNullOrEmpty(evt.Message.Text))
                return;

            var outcome = await _commandService.ProcessInputAsync(evt);
            if (!string.IsNullOrEmpty(rewardId))
                await TryCompletePendingRewardRedemptionAsync(evt, outcome);
        }
        catch (Exception ex)
        {
            _logger.Diagnostic(_diagnosticsService, AppDiagnosticSubsystem.Commands)
                .Error(AppDiagnosticCategory.InternalFault, "Unhandled exception while processing Twitch chat command input.")
                .WithDetail(ex.Message)
                .WithException(ex)
                .StatusOnly()
                .Write();
        }
    }

    private async void OnRewardRedeemed(object? sender, ChannelPointsCustomRewardRedemptionArgs args)
    {
        try
        {
            var evt = args.Notification.Payload.Event;
            if (!string.IsNullOrEmpty(evt.UserInput))
            {
                RegisterPendingRewardRedemption(evt.Id, evt.Reward.Id, evt.BroadcasterUserId, evt.UserId, evt.UserInput);
                return;
            }

            var outcome = await _commandService.ProcessRewardInputAsync(
                evt.Reward.Id,
                evt.BroadcasterUserId,
                evt.BroadcasterUserName,
                evt.BroadcasterUserLogin,
                evt.UserId,
                evt.UserName,
                evt.UserLogin);
            await TryFinalizeManagedRewardAsync(evt.Reward.Id, evt.Id, evt.BroadcasterUserId, outcome);
        }
        catch (Exception ex)
        {
            _logger.Diagnostic(_diagnosticsService, AppDiagnosticSubsystem.Commands)
                .Error(AppDiagnosticCategory.InternalFault, "Unhandled exception while processing Twitch reward command input.")
                .WithDetail(ex.Message)
                .WithException(ex)
                .StatusOnly()
                .Write();
        }
    }

    private void RegisterPendingRewardRedemption(string redemptionId, string rewardId, string broadcasterUserId, string userId, string userInput)
    {
        var key = BuildPendingRewardKey(rewardId, userId);
        if (_pendingRewardRedemptions.TryRemove(key, out var previous))
            previous.TimeoutCancellation.Dispose();

        var pending = new PendingRewardRedemption(redemptionId, rewardId, broadcasterUserId, userId, userInput, new CancellationTokenSource());
        _pendingRewardRedemptions[key] = pending;
        _ = MonitorPendingRewardTimeoutAsync(key, pending);
    }

    private async Task MonitorPendingRewardTimeoutAsync(string key, PendingRewardRedemption pending)
    {
        try
        {
            await Task.Delay(RewardCorrelationTimeout, pending.TimeoutCancellation.Token);
            if (!_pendingRewardRedemptions.TryRemove(key, out _))
                return;

            await TryFinalizeManagedRewardAsync(
                pending.RewardId,
                pending.RedemptionId,
                pending.BroadcasterUserId,
                new CommandExecutionOutcome
                {
                    CommandName = string.Empty,
                    Trigger = string.Empty,
                    InvocationKind = CommandInvocationKind.Reward,
                    Status = CommandExecutionStatus.Skipped,
                    Reason = CommandExecutionReason.TriggerNotMatched,
                    Message = "Reward correlation timed out before command input arrived."
                });
        }
        catch (OperationCanceledException)
        {
            // ignored
        }
        catch (Exception ex)
        {
            _logger.Diagnostic(_diagnosticsService, AppDiagnosticSubsystem.Commands)
                .Error(AppDiagnosticCategory.InternalFault, "Unhandled exception while monitoring Twitch reward correlation timeout.")
                .WithDetail(ex.Message)
                .WithException(ex)
                .DiagnosticsOnly()
                .Write();
        }
        finally
        {
            pending.TimeoutCancellation.Dispose();
        }
    }

    private async Task TryCompletePendingRewardRedemptionAsync(ChannelChatMessage evt, CommandExecutionOutcome outcome)
    {
        var rewardId = evt.ChannelPointsCustomRewardId;
        if (string.IsNullOrWhiteSpace(rewardId))
            return;

        var key = BuildPendingRewardKey(rewardId, evt.ChatterUserId);
        if (!_pendingRewardRedemptions.TryRemove(key, out var pending))
            return;

        pending.TimeoutCancellation.Cancel();
        pending.TimeoutCancellation.Dispose();
        await TryFinalizeManagedRewardAsync(rewardId, pending.RedemptionId, evt.BroadcasterUserId, outcome);
    }

    private async Task TryFinalizeManagedRewardAsync(string rewardId, string redemptionId, string broadcasterUserId, CommandExecutionOutcome outcome)
    {
        if (!TryGetRewardCommandConfiguration(rewardId, out var configuration))
            return;

        if (configuration.RewardTriggerMode != RewardTriggerMode.AppManaged)
            return;

        var status = outcome.IsFulfilled
            ? TwitchRedemptionStatus.Fulfilled
            : TwitchRedemptionStatus.Canceled;

        await _rewardService.UpdateRedemptionStatusAsync(
            await GetBroadcasterTokenAsync(),
            broadcasterUserId,
            rewardId,
            redemptionId,
            status);
    }

    private bool TryGetRewardCommandConfiguration(string rewardId, out CommandConfigurationSnapshot configuration)
    {
        configuration = null!;
        var match = _settingsService.GetCommandConfigurations()
            .FirstOrDefault(x => string.Equals(x.Value.RewardBinding?.RewardId, rewardId, StringComparison.Ordinal));
        if (string.IsNullOrWhiteSpace(match.Key))
            return false;

        configuration = match.Value;
        return true;
    }

    private static string BuildPendingRewardKey(string rewardId, string userId) => $"{rewardId}:{userId}";

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        StopTokenValidationLoop();
        _eventSubService.OnChatMessage -= OnChatMessage;
        _eventSubService.OnRewardRedeemed -= OnRewardRedeemed;
        foreach (var pending in _pendingRewardRedemptions.Values)
            pending.TimeoutCancellation.Cancel();
        _pendingRewardRedemptions.Clear();
        _sessionLock.Dispose();
    }

    private sealed record PendingRewardRedemption(
        string RedemptionId,
        string RewardId,
        string BroadcasterUserId,
        string UserId,
        string UserInput,
        CancellationTokenSource TimeoutCancellation);
}
