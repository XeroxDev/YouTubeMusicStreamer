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

using System.ComponentModel;
using Microsoft.Extensions.Logging.Abstractions;
using YouTubeMusicStreamer.Enums;
using YouTubeMusicStreamer.Services.App.Diagnostics;
using YouTubeMusicStreamer.Services.App.Persistence;
using YouTubeMusicStreamer.Services.Twitch;
using YouTubeMusicStreamer.Services.Twitch.Interfaces;
using YouTubeMusicStreamer.Tests.TestSupport;

namespace YouTubeMusicStreamer.Tests.Services.Twitch;

public sealed class TwitchServiceTests
{
    [Fact]
    public void Constructor_MapsCurrentCoordinatorState_ToServiceProperties()
    {
        var coordinator = new FakeTwitchSessionCoordinator(
            CreateState(
                TwitchSessionStatus.Ready,
                broadcasterIdentity: new TwitchIdentitySnapshot("broadcaster-id", "broadcaster", "Broadcaster", "broadcaster.png"),
                botIdentity: new TwitchIdentitySnapshot("bot-id", "bot", "Bot", "bot.png"),
                effectiveChatRole: TwitchAccountRole.Bot,
                rewards: CreateRewards(2)));
        var diagnostics = new RecordingDiagnosticsService();

        using var service = CreateService(coordinator, diagnostics);

        Assert.Equal(ConnectorState.LoggedIn, service.State);
        Assert.Equal(TwitchSessionStatus.Ready, service.SessionStatus);
        Assert.Equal("Broadcaster", service.Username);
        Assert.Equal("broadcaster-id", service.BroadcasterAccountId);
        Assert.Equal("Bot", service.BotUsername);
        Assert.Equal("broadcaster.png", service.ProfileImageUrl);
        Assert.Equal("bot.png", service.BotProfileImageUrl);
        Assert.Equal(TwitchAccountRole.Bot, service.EffectiveChatRole);
        Assert.Equal(2, service.Rewards.Count);
    }

    [Fact]
    public async Task InitializeIfNeededAsync_SetsLoadingBeforeCoordinatorInitialization_WhenNotAuthenticating()
    {
        var coordinator = new FakeTwitchSessionCoordinator(CreateState(TwitchSessionStatus.LoggedOut));
        var diagnostics = new RecordingDiagnosticsService();
        using var service = CreateService(coordinator, diagnostics);

        var statesSeen = new List<ConnectorState>();
        service.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(TwitchService.State))
                statesSeen.Add(service.State);
        };

        await service.InitializeIfNeededAsync();

        Assert.Contains(ConnectorState.Loading, statesSeen);
        Assert.Equal(1, coordinator.InitializeIfNeededCallCount);
    }

    [Fact]
    public async Task StartOAuthFlowAsync_TracksAuthState_AndReportsFailureMessage()
    {
        var coordinator = new FakeTwitchSessionCoordinator(CreateState(TwitchSessionStatus.LoggedOut))
        {
            BroadcasterAuthResult = new TwitchSessionFailure(TwitchFailureKind.AuthValidation, "No callback received")
        };
        var diagnostics = new RecordingDiagnosticsService();
        using var service = CreateService(coordinator, diagnostics);
        string? errorMessage = null;

        await service.StartOAuthFlowAsync(message => errorMessage = message);

        Assert.Equal("No callback received", errorMessage);
        Assert.Equal(1, coordinator.StartBroadcasterAuthCallCount);
        Assert.False(service.IsAuthInProgress);
        Assert.Null(service.PendingAuthRole);
        Assert.Equal(ConnectorState.LoggedOut, service.State);
    }

    [Fact]
    public async Task StartOAuthFlowAsync_RecordsDiagnostic_WhenCoordinatorThrows()
    {
        var coordinator = new FakeTwitchSessionCoordinator(CreateState(TwitchSessionStatus.LoggedOut))
        {
            BroadcasterAuthException = new InvalidOperationException("boom")
        };
        var diagnostics = new RecordingDiagnosticsService();
        using var service = CreateService(coordinator, diagnostics);
        string? errorMessage = null;

        await service.StartOAuthFlowAsync(message => errorMessage = message);

        Assert.Equal("Unexpected error during OAuth flow", errorMessage);
        var diagnostic = Assert.Single(diagnostics.RecentDiagnostics);
        Assert.Equal(AppDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal(AppDiagnosticCategory.Auth, diagnostic.Category);
        Assert.Equal("Twitch broadcaster authentication failed.", diagnostic.Summary);
        Assert.Equal(AppDiagnosticVisibility.Toast, diagnostic.Visibility);
    }

    [Fact]
    public async Task StartBotOAuthFlowAsync_TracksBotAuthState_AndReportsUnexpectedError()
    {
        var coordinator = new FakeTwitchSessionCoordinator(CreateState(TwitchSessionStatus.LoggedOut))
        {
            BotAuthException = new InvalidOperationException("bot boom")
        };
        var diagnostics = new RecordingDiagnosticsService();
        using var service = CreateService(coordinator, diagnostics);
        string? errorMessage = null;

        await service.StartBotOAuthFlowAsync(message => errorMessage = message);

        Assert.Equal("Unexpected error during bot OAuth flow", errorMessage);
        Assert.Equal(1, coordinator.StartBotAuthCallCount);
        Assert.False(service.IsAuthInProgress);
        Assert.Null(service.PendingAuthRole);
        var diagnostic = Assert.Single(diagnostics.RecentDiagnostics);
        Assert.Equal("Twitch bot authentication failed.", diagnostic.Summary);
    }

    [Fact]
    public async Task CancelPendingAuth_CancelsCoordinatorToken_WhenAuthIsInProgress()
    {
        var coordinator = new FakeTwitchSessionCoordinator(CreateState(TwitchSessionStatus.LoggedOut));
        coordinator.BroadcasterAuthResultProvider = async token =>
        {
            coordinator.CapturedBroadcasterAuthToken = token;
            try
            {
                await Task.Delay(5000, token);
                return null;
            }
            catch (OperationCanceledException)
            {
                coordinator.WasBroadcasterAuthCanceled = true;
                throw;
            }
        };
        var diagnostics = new RecordingDiagnosticsService();
        using var service = CreateService(coordinator, diagnostics);

        var authTask = service.StartOAuthFlowAsync(_ => { });
        await coordinator.BroadcasterAuthStarted.Task;
        service.CancelPendingAuth();
        await authTask;

        Assert.True(coordinator.WasBroadcasterAuthCanceled);
        Assert.False(service.IsAuthInProgress);
        Assert.Empty(diagnostics.RecentDiagnostics);
    }

    [Fact]
    public async Task CancelPendingAuth_DoesNotRecordDiagnostic_WhenBotAuthIsCanceled()
    {
        var coordinator = new FakeTwitchSessionCoordinator(CreateState(TwitchSessionStatus.LoggedOut));
        coordinator.BotAuthResultProvider = async token =>
        {
            try
            {
                await Task.Delay(5000, token);
                return null;
            }
            catch (OperationCanceledException)
            {
                coordinator.WasBotAuthCanceled = true;
                throw;
            }
        };
        var diagnostics = new RecordingDiagnosticsService();
        using var service = CreateService(coordinator, diagnostics);

        var authTask = service.StartBotOAuthFlowAsync(_ => { });
        await coordinator.BotAuthStarted.Task;
        service.CancelPendingAuth();
        await authTask;

        Assert.True(coordinator.WasBotAuthCanceled);
        Assert.False(service.IsAuthInProgress);
        Assert.Empty(diagnostics.RecentDiagnostics);
    }

    [Fact]
    public async Task StartOAuthFlowAsync_IgnoresSecondAuthStartAttempt_WhileAuthIsAlreadyInProgress()
    {
        var coordinator = new FakeTwitchSessionCoordinator(CreateState(TwitchSessionStatus.LoggedOut));
        coordinator.BroadcasterAuthResultProvider = async token =>
        {
            await Task.Delay(5000, token);
            return null;
        };

        using var service = CreateService(coordinator, new RecordingDiagnosticsService());

        var firstAuthTask = service.StartOAuthFlowAsync(_ => { });
        await coordinator.BroadcasterAuthStarted.Task;

        await service.StartOAuthFlowAsync(_ => { });

        Assert.Equal(1, coordinator.StartBroadcasterAuthCallCount);

        service.CancelPendingAuth();
        await firstAuthTask;
    }

    [Fact]
    public async Task StartBotOAuthFlowAsync_IgnoresSecondAuthStartAttempt_WhileBroadcasterAuthIsAlreadyInProgress()
    {
        var coordinator = new FakeTwitchSessionCoordinator(CreateState(TwitchSessionStatus.LoggedOut));
        coordinator.BroadcasterAuthResultProvider = async token =>
        {
            await Task.Delay(5000, token);
            return null;
        };

        using var service = CreateService(coordinator, new RecordingDiagnosticsService());

        var firstAuthTask = service.StartOAuthFlowAsync(_ => { });
        await coordinator.BroadcasterAuthStarted.Task;

        await service.StartBotOAuthFlowAsync(_ => { });

        Assert.Equal(1, coordinator.StartBroadcasterAuthCallCount);
        Assert.Equal(0, coordinator.StartBotAuthCallCount);
        Assert.Equal(TwitchAccountRole.Broadcaster, service.PendingAuthRole);

        service.CancelPendingAuth();
        await firstAuthTask;
    }

    [Fact]
    public async Task StateChanged_KeepsLoggingInState_WhileAuthIsStillInProgress()
    {
        var coordinator = new FakeTwitchSessionCoordinator(CreateState(TwitchSessionStatus.LoggedOut));
        coordinator.BroadcasterAuthResultProvider = async token =>
        {
            await Task.Delay(5000, token);
            return null;
        };

        using var service = CreateService(coordinator, new RecordingDiagnosticsService());

        var authTask = service.StartOAuthFlowAsync(_ => { });
        await coordinator.BroadcasterAuthStarted.Task;
        coordinator.RaiseStateChanged(CreateState(TwitchSessionStatus.Ready));

        Assert.True(service.IsAuthInProgress);
        Assert.Equal(ConnectorState.LoggingIn, service.State);

        service.CancelPendingAuth();
        await authTask;
    }

    [Fact]
    public async Task RefreshRewardsAsync_RecordsStatusOnlyDiagnostic_WhenCoordinatorThrows()
    {
        var coordinator = new FakeTwitchSessionCoordinator(CreateState(TwitchSessionStatus.Ready))
        {
            RefreshRewardsException = new InvalidOperationException("refresh failed")
        };
        var diagnostics = new RecordingDiagnosticsService();
        using var service = CreateService(coordinator, diagnostics);

        await service.RefreshRewardsAsync();

        var diagnostic = Assert.Single(diagnostics.RecentDiagnostics);
        Assert.Equal(AppDiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal(AppDiagnosticCategory.Configuration, diagnostic.Category);
        Assert.Equal(AppDiagnosticVisibility.StatusOnly, diagnostic.Visibility);
        Assert.Equal("Failed to refresh Twitch rewards.", diagnostic.Summary);
    }

    [Fact]
    public async Task RefreshManagedRewardBindingAsync_RecordsDiagnosticAndRethrows_WhenCoordinatorThrows()
    {
        var coordinator = new FakeTwitchSessionCoordinator(CreateState(TwitchSessionStatus.Ready))
        {
            RefreshManagedRewardException = new InvalidOperationException("sync failed")
        };
        var diagnostics = new RecordingDiagnosticsService();
        using var service = CreateService(coordinator, diagnostics);
        var binding = CreateBinding();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RefreshManagedRewardBindingAsync(binding, commandRequiresInput: true));

        Assert.Equal("sync failed", exception.Message);
        var diagnostic = Assert.Single(diagnostics.RecentDiagnostics);
        Assert.Equal("Failed to refresh managed reward binding.", diagnostic.Summary);
    }

    [Fact]
    public async Task CreateAndUpdateManagedRewardAsync_ForwardResults_WhenCoordinatorSucceeds()
    {
        var expectedRefresh = new ManagedRewardSyncResult(CreateBinding("refresh-reward"), true);
        var expectedCreate = new ManagedRewardSyncResult(CreateBinding("create-reward"), false);
        var expectedUpdate = new ManagedRewardSyncResult(CreateBinding("update-reward"), true);
        var coordinator = new FakeTwitchSessionCoordinator(CreateState(TwitchSessionStatus.Ready))
        {
            RefreshManagedRewardResult = expectedRefresh,
            CreateManagedRewardResult = expectedCreate,
            UpdateManagedRewardResult = expectedUpdate
        };

        using var service = CreateService(coordinator, new RecordingDiagnosticsService());

        var refresh = await service.RefreshManagedRewardBindingAsync(CreateBinding(), commandRequiresInput: true);
        var create = await service.CreateManagedRewardAsync(CreateBinding(), commandRequiresInput: false);
        var update = await service.UpdateManagedRewardAsync(CreateBinding(), commandRequiresInput: true);

        Assert.Equal(expectedRefresh, refresh);
        Assert.Equal(expectedCreate, create);
        Assert.Equal(expectedUpdate, update);
    }

    [Fact]
    public async Task LogoutAndDisconnectBot_RecordToastDiagnostics_WhenCoordinatorThrows()
    {
        var coordinator = new FakeTwitchSessionCoordinator(CreateState(TwitchSessionStatus.Ready))
        {
            LogoutException = new InvalidOperationException("logout failed"),
            DisconnectBotException = new InvalidOperationException("disconnect failed")
        };
        var diagnostics = new RecordingDiagnosticsService();
        using var service = CreateService(coordinator, diagnostics);

        await service.LogoutAsync();
        await service.DisconnectBotAsync();

        Assert.Equal(2, diagnostics.RecentDiagnostics.Count);
        Assert.Contains(diagnostics.RecentDiagnostics, diagnostic => diagnostic.Summary == "Failed to log out from Twitch.");
        Assert.Contains(diagnostics.RecentDiagnostics, diagnostic => diagnostic.Summary == "Failed to disconnect Twitch bot.");
        Assert.All(diagnostics.RecentDiagnostics, diagnostic => Assert.Equal(AppDiagnosticVisibility.Toast, diagnostic.Visibility));
    }

    [Fact]
    public void SessionFaulted_MapsFailuresToDiagnostics_AndIgnoresAuthCancelled()
    {
        var coordinator = new FakeTwitchSessionCoordinator(CreateState(TwitchSessionStatus.Ready));
        var diagnostics = new RecordingDiagnosticsService();
        using var service = CreateService(coordinator, diagnostics);

        coordinator.RaiseSessionFault(new TwitchSessionFailure(TwitchFailureKind.ChatTransport, "Chat failed"));
        coordinator.RaiseSessionFault(new TwitchSessionFailure(TwitchFailureKind.AccountReset, "Reset required"));
        coordinator.RaiseSessionFault(new TwitchSessionFailure(TwitchFailureKind.AuthCancelled, "Canceled"));

        Assert.Equal(2, diagnostics.RecentDiagnostics.Count);

        var chatDiagnostic = diagnostics.RecentDiagnostics.Single(diagnostic => diagnostic.Summary == "Chat failed");
        Assert.Equal(AppDiagnosticSeverity.Warning, chatDiagnostic.Severity);
        Assert.Equal(AppDiagnosticCategory.Connectivity, chatDiagnostic.Category);
        Assert.Equal(AppDiagnosticVisibility.StatusOnly, chatDiagnostic.Visibility);

        var resetDiagnostic = diagnostics.RecentDiagnostics.Single(diagnostic => diagnostic.Summary == "Reset required");
        Assert.Equal(AppDiagnosticSeverity.Error, resetDiagnostic.Severity);
        Assert.Equal(AppDiagnosticCategory.Persistence, resetDiagnostic.Category);
        Assert.Equal(AppDiagnosticVisibility.Toast, resetDiagnostic.Visibility);
    }

    [Fact]
    public void StateChanged_UpdatesServiceProperties_AndDisposeStopsFurtherUpdates()
    {
        var coordinator = new FakeTwitchSessionCoordinator(CreateState(TwitchSessionStatus.LoggedOut));
        using var service = CreateService(coordinator, new RecordingDiagnosticsService());
        var propertyChanges = new List<string>();
        service.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is not null)
                propertyChanges.Add(args.PropertyName);
        };

        coordinator.RaiseStateChanged(
            CreateState(
                TwitchSessionStatus.Degraded,
                broadcasterIdentity: new TwitchIdentitySnapshot("broadcaster-id", "broadcaster", "Broadcaster", null),
                botIdentity: new TwitchIdentitySnapshot("bot-id", "bot", "Bot", null),
                effectiveChatRole: TwitchAccountRole.Bot,
                rewards: CreateRewards(1)));

        Assert.Equal(TwitchSessionStatus.Degraded, service.SessionStatus);
        Assert.Equal(ConnectorState.LoggedIn, service.State);
        Assert.Equal("Broadcaster", service.Username);
        Assert.Equal("Bot", service.BotUsername);
        Assert.Single(service.Rewards);
        Assert.Contains(nameof(TwitchService.SessionStatus), propertyChanges);
        Assert.Contains(nameof(TwitchService.State), propertyChanges);

        service.Dispose();
        coordinator.RaiseStateChanged(CreateState(TwitchSessionStatus.Error));

        Assert.Equal(TwitchSessionStatus.Degraded, service.SessionStatus);
    }

    private static TwitchService CreateService(FakeTwitchSessionCoordinator coordinator, RecordingDiagnosticsService diagnostics) =>
        new(coordinator, NullLogger<TwitchService>.Instance, diagnostics);

    private static TwitchSessionState CreateState(
        TwitchSessionStatus status,
        TwitchIdentitySnapshot? broadcasterIdentity = null,
        TwitchIdentitySnapshot? botIdentity = null,
        TwitchAccountRole effectiveChatRole = TwitchAccountRole.Broadcaster,
        IReadOnlyList<TwitchRewardSnapshot>? rewards = null) =>
        new(
            status,
            new TwitchAccountSession(TwitchAccountRole.Broadcaster, true, true, true, broadcasterIdentity),
            botIdentity is null ? null : new TwitchAccountSession(TwitchAccountRole.Bot, true, true, true, botIdentity),
            effectiveChatRole,
            rewards ?? [],
            null);

    private static IReadOnlyList<TwitchRewardSnapshot> CreateRewards(int count)
    {
        var list = new List<TwitchRewardSnapshot>(count);
        for (var i = 0; i < count; i++)
        {
            list.Add(new TwitchRewardSnapshot($"reward-{i}", $"Reward {i}", $"Prompt {i}", (uint)(100 + i), i % 2 == 0));
        }

        return list;
    }

    private static CommandRewardBindingSnapshot CreateBinding(string rewardId = "reward-id") => new()
    {
        RewardId = rewardId,
        BroadcasterAccountId = "broadcaster-id",
        ManagedRewardName = "Reward",
        ManagedRewardPrompt = "Prompt",
        ManagedRewardCost = 100,
        ManagedRewardRequiresUserInput = true,
        ManagedRewardCompatibilityState = ManagedRewardCompatibilityState.Compatible
    };

    private sealed class FakeTwitchSessionCoordinator : ITwitchSessionCoordinator
    {
        public FakeTwitchSessionCoordinator(TwitchSessionState currentState)
        {
            CurrentState = currentState;
        }

        public TwitchSessionState CurrentState { get; private set; }
        public event EventHandler<TwitchSessionState> StateChanged = delegate { };
        public event EventHandler<TwitchSessionFailure> SessionFaulted = delegate { };

        public int InitializeIfNeededCallCount { get; private set; }
        public int StartBroadcasterAuthCallCount { get; private set; }
        public int StartBotAuthCallCount { get; private set; }

        public TwitchSessionFailure? BroadcasterAuthResult { get; set; }
        public TwitchSessionFailure? BotAuthResult { get; set; }
        public Exception? BroadcasterAuthException { get; set; }
        public Exception? BotAuthException { get; set; }
        public Func<CancellationToken, Task<TwitchSessionFailure?>>? BroadcasterAuthResultProvider { get; set; }
        public Func<CancellationToken, Task<TwitchSessionFailure?>>? BotAuthResultProvider { get; set; }
        public CancellationToken CapturedBroadcasterAuthToken { get; set; }
        public bool WasBroadcasterAuthCanceled { get; set; }
        public bool WasBotAuthCanceled { get; set; }
        public TaskCompletionSource BroadcasterAuthStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource BotAuthStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Exception? RefreshRewardsException { get; set; }
        public Exception? RefreshManagedRewardException { get; set; }
        public Exception? CreateManagedRewardException { get; set; }
        public Exception? UpdateManagedRewardException { get; set; }
        public Exception? LogoutException { get; set; }
        public Exception? DisconnectBotException { get; set; }
        public ManagedRewardSyncResult RefreshManagedRewardResult { get; set; } = new(CreateBinding(), true);
        public ManagedRewardSyncResult CreateManagedRewardResult { get; set; } = new(CreateBinding(), true);
        public ManagedRewardSyncResult UpdateManagedRewardResult { get; set; } = new(CreateBinding(), true);

        public Task InitializeIfNeededAsync()
        {
            InitializeIfNeededCallCount++;
            return Task.CompletedTask;
        }

        public async Task<TwitchSessionFailure?> StartBroadcasterAuthAsync(CancellationToken cancellationToken = default)
        {
            StartBroadcasterAuthCallCount++;
            BroadcasterAuthStarted.TrySetResult();
            CapturedBroadcasterAuthToken = cancellationToken;

            if (BroadcasterAuthResultProvider is not null)
                return await BroadcasterAuthResultProvider(cancellationToken);

            if (BroadcasterAuthException is not null)
                throw BroadcasterAuthException;

            return BroadcasterAuthResult;
        }

        public async Task<TwitchSessionFailure?> StartBotAuthAsync(CancellationToken cancellationToken = default)
        {
            StartBotAuthCallCount++;
            BotAuthStarted.TrySetResult();

            if (BotAuthResultProvider is not null)
                return await BotAuthResultProvider(cancellationToken);

            if (BotAuthException is not null)
                throw BotAuthException;

            return BotAuthResult;
        }

        public Task DisconnectBotAsync()
        {
            if (DisconnectBotException is not null)
                throw DisconnectBotException;

            return Task.CompletedTask;
        }

        public Task LogoutAsync()
        {
            if (LogoutException is not null)
                throw LogoutException;

            return Task.CompletedTask;
        }

        public Task RefreshRewardsAsync()
        {
            if (RefreshRewardsException is not null)
                throw RefreshRewardsException;

            return Task.CompletedTask;
        }

        public Task<ManagedRewardSyncResult> RefreshManagedRewardBindingAsync(
            CommandRewardBindingSnapshot binding,
            bool commandRequiresInput,
            CancellationToken cancellationToken = default)
        {
            if (RefreshManagedRewardException is not null)
                throw RefreshManagedRewardException;

            return Task.FromResult(RefreshManagedRewardResult);
        }

        public Task<ManagedRewardSyncResult> CreateManagedRewardAsync(
            CommandRewardBindingSnapshot binding,
            bool commandRequiresInput,
            CancellationToken cancellationToken = default)
        {
            if (CreateManagedRewardException is not null)
                throw CreateManagedRewardException;

            return Task.FromResult(CreateManagedRewardResult);
        }

        public Task<ManagedRewardSyncResult> UpdateManagedRewardAsync(
            CommandRewardBindingSnapshot binding,
            bool commandRequiresInput,
            CancellationToken cancellationToken = default)
        {
            if (UpdateManagedRewardException is not null)
                throw UpdateManagedRewardException;

            return Task.FromResult(UpdateManagedRewardResult);
        }

        public void RaiseStateChanged(TwitchSessionState state)
        {
            CurrentState = state;
            StateChanged(this, state);
        }

        public void RaiseSessionFault(TwitchSessionFailure failure) => SessionFaulted(this, failure);
    }
}
