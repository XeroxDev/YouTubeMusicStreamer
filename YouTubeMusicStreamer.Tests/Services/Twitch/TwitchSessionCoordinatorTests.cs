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

using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;
using YouTubeMusicStreamer.Services.App;
using YouTubeMusicStreamer.Services.App.Diagnostics;
using YouTubeMusicStreamer.Services.App.Persistence;
using YouTubeMusicStreamer.Services.Commands;
using YouTubeMusicStreamer.Services.Commands.ArgumentParser;
using YouTubeMusicStreamer.Services.Commands.Binding;
using YouTubeMusicStreamer.Services.Commands.Cooldowns;
using YouTubeMusicStreamer.Services.Commands.Formatting;
using YouTubeMusicStreamer.Services.Commands.Placeholders;
using YouTubeMusicStreamer.Services.Commands.PrerequisiteChecking;
using YouTubeMusicStreamer.Services.Twitch;
using YouTubeMusicStreamer.Services.Twitch.Implementations;
using YouTubeMusicStreamer.Services.Twitch.Interfaces;
using YouTubeMusicStreamer.Tests.TestSupport;

namespace YouTubeMusicStreamer.Tests.Services.Twitch;

public sealed class TwitchSessionCoordinatorTests
{
    [Fact]
    public async Task InitializeIfNeededAsync_ClearsInvalidBroadcasterToken_AndPreservesExistingBotToken()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        await harness.Settings.InitializeAsync();
        await harness.Settings.SaveSensitiveSettingAsync(s =>
        {
            s.TwitchBroadcasterAccessToken = "bad-broadcaster";
            s.TwitchBotAccessToken = "existing-bot";
        });

        await using var fixture = await CreateFixtureAsync(harness.Settings);
        fixture.TokenService.ValidateResults["bad-broadcaster"] = false;
        TwitchSessionFailure? observedFailure = null;
        fixture.Coordinator.SessionFaulted += (_, failure) => observedFailure = failure;

        await fixture.Coordinator.InitializeIfNeededAsync();

        var sensitive = await harness.Settings.GetSensitiveSettingsAsync();
        Assert.Null(sensitive.TwitchBroadcasterAccessToken);
        Assert.Null(sensitive.TwitchAccessToken);
        Assert.Equal("existing-bot", sensitive.TwitchBotAccessToken);
        Assert.Equal(TwitchSessionStatus.LoggedOut, fixture.Coordinator.CurrentState.Status);
        Assert.Equal(TwitchFailureKind.AuthValidation, observedFailure?.Kind);
        Assert.Empty(fixture.ChatService.ConnectCalls);
    }

    [Fact]
    public async Task StartBroadcasterAuthAsync_ReturnsCancelledFailure_AndDoesNotConnectAnything()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        await harness.Settings.InitializeAsync();

        await using var fixture = await CreateFixtureAsync(harness.Settings);
        fixture.AuthService.Result = new TwitchAuthResult(TwitchAuthStatus.Cancelled, null, "User closed auth");

        var failure = await fixture.Coordinator.StartBroadcasterAuthAsync();

        Assert.NotNull(failure);
        Assert.Equal(TwitchFailureKind.AuthCancelled, failure!.Kind);
        Assert.Equal(TwitchSessionStatus.LoggedOut, fixture.Coordinator.CurrentState.Status);
        Assert.Empty(fixture.ChatService.ConnectCalls);
        Assert.Equal(0, fixture.EventSubService.StartCallCount);
    }

    [Fact]
    public async Task StartBroadcasterAuthAsync_Degrades_WhenRewardLoadFails_ButStillConnectsCoreSession()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        await harness.Settings.InitializeAsync();

        await using var fixture = await CreateFixtureAsync(harness.Settings);
        fixture.AuthService.Result = new TwitchAuthResult(TwitchAuthStatus.Success, "broadcaster-token", null);
        fixture.TokenService.ValidateResults["broadcaster-token"] = true;
        fixture.UserService.Identities["broadcaster-token"] = new TwitchUserIdentity("b-id", "broadcaster", "Broadcaster", "img.png");
        fixture.UserService.GetRewardsException = new InvalidOperationException("reward api failed");

        var failure = await fixture.Coordinator.StartBroadcasterAuthAsync();

        var sensitive = await harness.Settings.GetSensitiveSettingsAsync();
        var twitchSettings = harness.Settings.GetTwitchSettings();

        Assert.NotNull(failure);
        Assert.Equal(TwitchFailureKind.RewardLoad, failure!.Kind);
        Assert.Equal(TwitchSessionStatus.Degraded, fixture.Coordinator.CurrentState.Status);
        Assert.Equal(1, fixture.EventSubService.StartCallCount);
        Assert.Single(fixture.ChatService.ConnectCalls);
        Assert.Equal("broadcaster", fixture.ChatService.ConnectCalls[0].Username);
        Assert.Equal("broadcaster-token", sensitive.TwitchBroadcasterAccessToken);
        Assert.Equal("b-id", twitchSettings.BroadcasterAccount?.AccountId);
        Assert.Empty(fixture.Coordinator.CurrentState.Rewards);
    }

    [Fact]
    public async Task StartBotAuthAsync_FailsFast_WhenNoBroadcasterSessionExists()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        await harness.Settings.InitializeAsync();

        await using var fixture = await CreateFixtureAsync(harness.Settings);

        var failure = await fixture.Coordinator.StartBotAuthAsync();

        Assert.NotNull(failure);
        Assert.Equal(TwitchFailureKind.AuthValidation, failure!.Kind);
        Assert.Equal(TwitchSessionStatus.AuthRequired, fixture.Coordinator.CurrentState.Status);
        Assert.Empty(fixture.AuthService.StartCalls);
    }

    [Fact]
    public async Task StartBotAuthAsync_ClearsInvalidBotToken_AndFallsBackToBroadcasterChat()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        await harness.Settings.InitializeAsync();
        await harness.Settings.SaveSensitiveSettingAsync(s => s.TwitchBroadcasterAccessToken = "broadcaster-token");

        await using var fixture = await CreateFixtureAsync(harness.Settings);
        fixture.TokenService.ValidateResults["broadcaster-token"] = true;
        fixture.UserService.Identities["broadcaster-token"] = new TwitchUserIdentity("b-id", "broadcaster", "Broadcaster", null);
        fixture.UserService.Rewards["b-id"] = [];
        fixture.AuthService.Result = new TwitchAuthResult(TwitchAuthStatus.Success, "bad-bot-token", null);
        fixture.TokenService.ValidateResults["bad-bot-token"] = false;

        var initialFailure = await fixture.Coordinator.InitializeIfNeededAsyncOrThrowIfFaulted();
        Assert.Null(initialFailure);

        var failure = await fixture.Coordinator.StartBotAuthAsync();

        var sensitive = await harness.Settings.GetSensitiveSettingsAsync();

        Assert.NotNull(failure);
        Assert.Equal(TwitchFailureKind.AuthValidation, failure!.Kind);
        Assert.Null(sensitive.TwitchBotAccessToken);
        Assert.Equal(TwitchSessionStatus.Degraded, fixture.Coordinator.CurrentState.Status);
        Assert.Equal(TwitchAccountRole.Broadcaster, fixture.Coordinator.CurrentState.EffectiveChatRole);
        Assert.Equal(2, fixture.ChatService.ConnectCalls.Count);
        Assert.Equal("broadcaster", fixture.ChatService.ConnectCalls[^1].Username);
        Assert.Equal(2, fixture.ChatService.DisconnectCallCount);
    }

    [Fact]
    public async Task RefreshRewardsAsync_SyncsManagedRewardBindingCompatibility_IntoCommandConfiguration()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        await harness.Settings.InitializeAsync();
        await harness.Settings.SaveSensitiveSettingAsync(s => s.TwitchBroadcasterAccessToken = "broadcaster-token");

        await using var fixture = await CreateFixtureAsync(harness.Settings);
        fixture.TokenService.ValidateResults["broadcaster-token"] = true;
        fixture.UserService.Identities["broadcaster-token"] = new TwitchUserIdentity("b-id", "broadcaster", "Broadcaster", null);
        fixture.UserService.Rewards["b-id"] = [];

        var requestConfig = harness.Settings.GetCommandConfiguration("RequestCommand")!;
        requestConfig.ChatTriggerMode = ChatTriggerMode.Disabled;
        requestConfig.RewardTriggerMode = RewardTriggerMode.AppManaged;
        requestConfig.RewardBinding = new CommandRewardBindingSnapshot
        {
            RewardId = "reward-1",
            BroadcasterAccountId = "old-account",
            ManagedRewardName = "Old reward",
            ManagedRewardPrompt = "Old prompt",
            ManagedRewardCost = 10,
            ManagedRewardRequiresUserInput = false,
            ManagedRewardCompatibilityState = ManagedRewardCompatibilityState.Unknown
        };
        await harness.Settings.SaveCommandConfigurationAsync("RequestCommand", requestConfig);

        fixture.UserService.Rewards["b-id"] =
        [
            CreateReward("reward-1", "Managed reward", "Prompt", 500, false)
        ];

        var initialFailure = await fixture.Coordinator.InitializeIfNeededAsyncOrThrowIfFaulted();
        Assert.Null(initialFailure);

        await fixture.Coordinator.RefreshRewardsAsync();

        var updated = harness.Settings.GetCommandConfiguration("RequestCommand")!;
        Assert.NotNull(updated.RewardBinding);
        Assert.Equal("reward-1", updated.RewardBinding!.RewardId);
        Assert.Equal("Managed reward", updated.RewardBinding.ManagedRewardName);
        Assert.Equal("Prompt", updated.RewardBinding.ManagedRewardPrompt);
        Assert.Equal((uint)500, updated.RewardBinding.ManagedRewardCost);
        Assert.False(updated.RewardBinding.ManagedRewardRequiresUserInput);
        Assert.Equal(ManagedRewardCompatibilityState.Incompatible, updated.RewardBinding.ManagedRewardCompatibilityState);
    }

    [Fact]
    public async Task ValidateConfiguredTokensAsync_ClearsAllTokens_WhenBroadcasterTokenBecomesInvalid()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        await harness.Settings.InitializeAsync();
        await harness.Settings.SaveSensitiveSettingAsync(s =>
        {
            s.TwitchBroadcasterAccessToken = "broadcaster-token";
            s.TwitchBotAccessToken = "bot-token";
        });

        await using var fixture = await CreateFixtureAsync(harness.Settings);
        fixture.TokenService.ValidateResults["broadcaster-token"] = true;
        fixture.TokenService.ValidateResults["bot-token"] = true;
        fixture.UserService.Identities["broadcaster-token"] = new TwitchUserIdentity("b-id", "broadcaster", "Broadcaster", null);
        fixture.UserService.Rewards["b-id"] = [];

        var initialFailure = await fixture.Coordinator.InitializeIfNeededAsyncOrThrowIfFaulted();
        Assert.Null(initialFailure);

        fixture.TokenService.ValidateResults["broadcaster-token"] = false;

        var failure = await InvokeValidateConfiguredTokensAsync(fixture.Coordinator);

        var sensitive = await harness.Settings.GetSensitiveSettingsAsync();
        var twitchSettings = harness.Settings.GetTwitchSettings();

        Assert.NotNull(failure);
        Assert.Equal(TwitchFailureKind.AuthValidation, failure!.Kind);
        Assert.Null(sensitive.TwitchBroadcasterAccessToken);
        Assert.Null(sensitive.TwitchAccessToken);
        Assert.Null(sensitive.TwitchBotAccessToken);
        Assert.Null(twitchSettings.BroadcasterAccount);
        Assert.Null(twitchSettings.BotAccount);
        Assert.Equal(TwitchSessionStatus.LoggedOut, fixture.Coordinator.CurrentState.Status);
    }

    [Fact]
    public async Task ValidateConfiguredTokensAsync_ClearsOnlyBotToken_AndReconnectsBroadcasterChat_WhenBotTokenBecomesInvalid()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        await harness.Settings.InitializeAsync();
        await harness.Settings.SaveSensitiveSettingAsync(s =>
        {
            s.TwitchBroadcasterAccessToken = "broadcaster-token";
            s.TwitchBotAccessToken = "bot-token";
        });

        await using var fixture = await CreateFixtureAsync(harness.Settings);
        fixture.TokenService.ValidateResults["broadcaster-token"] = true;
        fixture.TokenService.ValidateResults["bot-token"] = true;
        fixture.UserService.Identities["broadcaster-token"] = new TwitchUserIdentity("b-id", "broadcaster", "Broadcaster", null);
        fixture.UserService.Identities["bot-token"] = new TwitchUserIdentity("bot-id", "bot", "Bot", null);
        fixture.UserService.Rewards["b-id"] = [];
        fixture.AuthService.Result = new TwitchAuthResult(TwitchAuthStatus.Success, "bot-token", null);

        var initialFailure = await fixture.Coordinator.InitializeIfNeededAsyncOrThrowIfFaulted();
        Assert.Null(initialFailure);
        var botFailure = await fixture.Coordinator.StartBotAuthAsyncWithResult("bot-token");
        Assert.Null(botFailure);

        fixture.TokenService.ValidateResults["bot-token"] = false;

        var failure = await InvokeValidateConfiguredTokensAsync(fixture.Coordinator);

        var sensitive = await harness.Settings.GetSensitiveSettingsAsync();
        var twitchSettings = harness.Settings.GetTwitchSettings();

        Assert.NotNull(failure);
        Assert.Equal(TwitchFailureKind.AuthValidation, failure!.Kind);
        Assert.Equal("broadcaster-token", sensitive.TwitchBroadcasterAccessToken);
        Assert.Null(sensitive.TwitchBotAccessToken);
        Assert.NotNull(twitchSettings.BroadcasterAccount);
        Assert.Null(twitchSettings.BotAccount);
        Assert.Equal(TwitchSessionStatus.Degraded, fixture.Coordinator.CurrentState.Status);
        Assert.Equal(TwitchAccountRole.Broadcaster, fixture.Coordinator.CurrentState.EffectiveChatRole);
        Assert.Equal(3, fixture.ChatService.ConnectCalls.Count);
        Assert.Equal("broadcaster", fixture.ChatService.ConnectCalls[^1].Username);
    }

    private static async Task<TwitchSessionFailure?> InvokeValidateConfiguredTokensAsync(TwitchSessionCoordinator coordinator)
    {
        TwitchSessionFailure? observed = null;
        void Handler(object? _, TwitchSessionFailure failure) => observed = failure;

        coordinator.SessionFaulted += Handler;
        try
        {
            var method = typeof(TwitchSessionCoordinator).GetMethod("ValidateConfiguredTokensAsync", BindingFlags.Instance | BindingFlags.NonPublic)
                         ?? throw new InvalidOperationException("ValidateConfiguredTokensAsync was not found.");

            var task = (Task)method.Invoke(coordinator, [CancellationToken.None])!;
            await task;
            return observed;
        }
        finally
        {
            coordinator.SessionFaulted -= Handler;
        }
    }

    private static async Task<CommandService> CreateCommandServiceAsync(SettingsService settings)
    {
        var registry = new CommandRegistry(settings, new ReflectionPlaceholderProvider(), NullLogger<CommandRegistry>.Instance);
        var service = new CommandService(
            settings,
            registry,
            new ServiceCollection().BuildServiceProvider(),
            new NoOpTwitchChatService(),
            new ArgumentParser(),
            new PrerequisiteChecker(),
            new CooldownManager(),
            new ReflectionArgumentBinder(),
            new ResponseFormatter(),
            NullLogger<CommandService>.Instance);

        await service.InitializeAsync();
        return service;
    }

    private static TwitchRewardSnapshot CreateReward(string id, string title, string prompt, int cost, bool requiresUserInput) =>
        new(id, title, prompt, checked((uint)cost), requiresUserInput);

    private static void SetAutoProperty<T>(object instance, string propertyName, T value)
    {
        var field = instance.GetType().GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException($"Backing field for '{propertyName}' was not found on '{instance.GetType().Name}'.");
        field.SetValue(instance, value);
    }

    private static async Task<TwitchSessionCoordinatorFixture> CreateFixtureAsync(SettingsService settings)
    {
        var authService = new FakeTwitchAuthService();
        var tokenService = new FakeTwitchTokenService();
        var userService = new FakeTwitchUserService();
        var rewardService = new FakeTwitchRewardService();
        var chatService = new FakeTwitchChatService();
        var eventSubService = new FakeTwitchEventSubService();
        var commandService = await CreateCommandServiceAsync(settings);
        var diagnostics = new RecordingDiagnosticsService();

        var coordinator = new TwitchSessionCoordinator(
            settings,
            authService,
            tokenService,
            userService,
            rewardService,
            chatService,
            eventSubService,
            commandService,
            diagnostics,
            NullLogger<TwitchSessionCoordinator>.Instance);

        return new TwitchSessionCoordinatorFixture(coordinator, authService, tokenService, userService, rewardService, chatService, eventSubService);
    }

    private sealed class TwitchSessionCoordinatorFixture : IAsyncDisposable
    {
        public TwitchSessionCoordinatorFixture(
            TwitchSessionCoordinator coordinator,
            FakeTwitchAuthService authService,
            FakeTwitchTokenService tokenService,
            FakeTwitchUserService userService,
            FakeTwitchRewardService rewardService,
            FakeTwitchChatService chatService,
            FakeTwitchEventSubService eventSubService)
        {
            Coordinator = coordinator;
            AuthService = authService;
            TokenService = tokenService;
            UserService = userService;
            RewardService = rewardService;
            ChatService = chatService;
            EventSubService = eventSubService;
        }

        public TwitchSessionCoordinator Coordinator { get; }
        public FakeTwitchAuthService AuthService { get; }
        public FakeTwitchTokenService TokenService { get; }
        public FakeTwitchUserService UserService { get; }
        public FakeTwitchRewardService RewardService { get; }
        public FakeTwitchChatService ChatService { get; }
        public FakeTwitchEventSubService EventSubService { get; }

        public async ValueTask DisposeAsync()
        {
            Coordinator.Dispose();
            await Task.CompletedTask;
        }
    }

    private sealed class FakeTwitchAuthService : ITwitchAuthService
    {
        public TwitchAuthResult Result { get; set; } = new(TwitchAuthStatus.Success, "token", null);
        public List<TwitchAccountRole> StartCalls { get; } = [];

        public Task<TwitchAuthResult> StartAsync(TwitchAccountRole role, CancellationToken cancellationToken = default)
        {
            StartCalls.Add(role);
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeTwitchTokenService : ITwitchTokenService
    {
        public Dictionary<string, bool> ValidateResults { get; } = new(StringComparer.Ordinal);
        public List<string> ValidateCalls { get; } = [];

        public Task<bool> ValidateAsync(string token)
        {
            ValidateCalls.Add(token);
            return Task.FromResult(ValidateResults.TryGetValue(token, out var result) && result);
        }
    }

    private sealed class FakeTwitchUserService : ITwitchUserService
    {
        public Dictionary<string, TwitchUserIdentity?> Identities { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, IReadOnlyList<TwitchRewardSnapshot>> Rewards { get; } = new(StringComparer.Ordinal);
        public Exception? GetRewardsException { get; set; }

        public Task<TwitchUserIdentity?> GetCurrentUserAsync(string accessToken) =>
            Task.FromResult(Identities.TryGetValue(accessToken, out var identity) ? identity : null);

        public Task<IReadOnlyList<TwitchRewardSnapshot>> GetRewardsAsync(string accessToken, string channelId)
        {
            if (GetRewardsException is not null)
                throw GetRewardsException;

            return Task.FromResult(Rewards.TryGetValue(channelId, out var rewards) ? rewards : (IReadOnlyList<TwitchRewardSnapshot>)[]);
        }
    }

    private sealed class FakeTwitchRewardService : ITwitchRewardService
    {
        public List<(string AccessToken, string BroadcasterId, string RewardId, string RedemptionId, TwitchRedemptionStatus Status)> RedemptionUpdates { get; } = [];

        public Task<TwitchManagedReward?> GetRewardByIdAsync(string accessToken, string broadcasterId, string rewardId, CancellationToken cancellationToken = default) =>
            Task.FromResult<TwitchManagedReward?>(null);

        public Task<TwitchManagedReward> CreateManagedRewardAsync(string accessToken, string broadcasterId, TwitchManagedRewardDefinition definition, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TwitchManagedReward> UpdateManagedRewardAsync(string accessToken, string broadcasterId, string rewardId, TwitchManagedRewardDefinition definition, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task UpdateRedemptionStatusAsync(string accessToken, string broadcasterId, string rewardId, string redemptionId, TwitchRedemptionStatus status, CancellationToken cancellationToken = default)
        {
            RedemptionUpdates.Add((accessToken, broadcasterId, rewardId, redemptionId, status));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTwitchChatService : ITwitchChatService
    {
        public List<(string Username, string AccessToken, string ChannelLogin)> ConnectCalls { get; } = [];
        public int DisconnectCallCount { get; private set; }

        public Task ConnectAsync(string username, string accessToken, string channelLogin)
        {
            ConnectCalls.Add((username, accessToken, channelLogin));
            return Task.CompletedTask;
        }

        public Task DisconnectAsync()
        {
            DisconnectCallCount++;
            return Task.CompletedTask;
        }

        public void SendMessage(ChannelChatMessage senderMessage, string message, bool asReply = true) { }
    }

    private sealed class FakeTwitchEventSubService : ITwitchEventSubService
    {
        public int StartCallCount { get; private set; }
        public int StopCallCount { get; private set; }

        public event EventHandler<TwitchLib.EventSub.Websockets.Core.EventArgs.Channel.ChannelChatMessageArgs> OnChatMessage = delegate { };
        public event EventHandler<TwitchLib.EventSub.Websockets.Core.EventArgs.Channel.ChannelPointsCustomRewardRedemptionArgs> OnRewardRedeemed = delegate { };

        public Task StartAsync(string channelId, string broadcasterAccessToken)
        {
            StartCallCount++;
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            StopCallCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpTwitchChatService : ITwitchChatService
    {
        public Task ConnectAsync(string username, string accessToken, string channelLogin) => Task.CompletedTask;
        public Task DisconnectAsync() => Task.CompletedTask;
        public void SendMessage(ChannelChatMessage senderMessage, string message, bool asReply = true) { }
    }
}

internal static class TwitchSessionCoordinatorTestExtensions
{
    public static async Task<TwitchSessionFailure?> InitializeIfNeededAsyncOrThrowIfFaulted(this TwitchSessionCoordinator coordinator)
    {
        TwitchSessionFailure? failure = null;
        void Handler(object? _, TwitchSessionFailure observed) => failure = observed;

        coordinator.SessionFaulted += Handler;
        try
        {
            await coordinator.InitializeIfNeededAsync();
            return failure;
        }
        finally
        {
            coordinator.SessionFaulted -= Handler;
        }
    }

    public static async Task<TwitchSessionFailure?> StartBotAuthAsyncWithResult(this TwitchSessionCoordinator coordinator, string expectedToken)
    {
        TwitchSessionFailure? failure = null;
        void Handler(object? _, TwitchSessionFailure observed) => failure = observed;

        coordinator.SessionFaulted += Handler;
        try
        {
            await coordinator.StartBotAuthAsync();
            return failure;
        }
        finally
        {
            coordinator.SessionFaulted -= Handler;
        }
    }
}
