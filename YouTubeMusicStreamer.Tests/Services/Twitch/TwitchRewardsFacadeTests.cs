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
using System.ComponentModel;
using Microsoft.Extensions.Logging.Abstractions;
using YouTubeMusicStreamer.Services.App;
using YouTubeMusicStreamer.Services.App.Diagnostics;
using YouTubeMusicStreamer.Services.App.Persistence;
using YouTubeMusicStreamer.Services.Twitch;
using YouTubeMusicStreamer.Services.Twitch.Interfaces;

namespace YouTubeMusicStreamer.Tests.Services.Twitch;

public sealed class TwitchRewardsFacadeTests
{
    [Fact]
    public void Initialize_IsIdempotent_AndCopiesCurrentRewards()
    {
        var coordinator = new FakeTwitchSessionCoordinator(CreateState(CreateRewards(1)));
        var service = CreateService(coordinator);
        var facade = new TwitchRewardsFacade(service);
        var stateChangedCount = 0;
        facade.StateChanged += (_, _) => stateChangedCount++;

        facade.Initialize();
        facade.Initialize();

        Assert.Single(facade.Rewards);

        SetRewards(service, CreateRewards(2));

        Assert.Equal(1, stateChangedCount);
        Assert.Equal(2, facade.Rewards.Count);
    }

    [Fact]
    public async Task RefreshAsync_TogglesStateAndUpdatesRewards_WhenCoordinatorRefreshes()
    {
        var coordinator = new FakeTwitchSessionCoordinator(CreateState(CreateRewards(1)));
        coordinator.NextRefreshState = CreateState(CreateRewards(2));
        var service = CreateService(coordinator);
        var facade = new TwitchRewardsFacade(service);
        var stateChangedCount = 0;
        facade.StateChanged += (_, _) => stateChangedCount++;

        facade.Initialize();

        var refreshTask = facade.RefreshAsync();
        await coordinator.RefreshStarted;

        Assert.True(facade.IsRefreshing);
        Assert.Equal(1, coordinator.RefreshRewardsCallCount);
        Assert.True(stateChangedCount >= 1);

        coordinator.AllowRefresh();
        await refreshTask;

        Assert.False(facade.IsRefreshing);
        Assert.True(stateChangedCount >= 2);
        Assert.Equal(2, facade.Rewards.Count);
    }

    [Fact]
    public async Task RefreshAsync_IgnoresSecondRefreshRequest_WhileRefreshIsAlreadyRunning()
    {
        var coordinator = new FakeTwitchSessionCoordinator(CreateState(CreateRewards(1)));
        var service = CreateService(coordinator);
        var facade = new TwitchRewardsFacade(service);
        facade.Initialize();

        var firstRefresh = facade.RefreshAsync();
        await coordinator.RefreshStarted;

        await facade.RefreshAsync();

        Assert.Equal(1, coordinator.RefreshRewardsCallCount);

        coordinator.AllowRefresh();
        await firstRefresh;
    }

    [Fact]
    public void RewardsPropertyChanged_UpdatesFacadeRewards_WhenServiceRaisesNotification()
    {
        var coordinator = new FakeTwitchSessionCoordinator(CreateState(CreateRewards(1)));
        var service = CreateService(coordinator);
        var facade = new TwitchRewardsFacade(service);
        var stateChangedCount = 0;
        facade.StateChanged += (_, _) => stateChangedCount++;

        facade.Initialize();

        SetRewards(service, CreateRewards(3));

        Assert.Equal(1, stateChangedCount);
        Assert.Equal(3, facade.Rewards.Count);
        Assert.False(facade.IsRefreshing);
    }

    [Fact]
    public void Dispose_UnsubscribesFromTwitchServicePropertyChanged()
    {
        var coordinator = new FakeTwitchSessionCoordinator(CreateState(CreateRewards(1)));
        var service = CreateService(coordinator);
        var facade = new TwitchRewardsFacade(service);
        var stateChangedCount = 0;
        facade.StateChanged += (_, _) => stateChangedCount++;

        facade.Initialize();
        facade.Dispose();

        SetRewards(service, CreateRewards(4));

        Assert.Equal(0, stateChangedCount);
        Assert.Single(facade.Rewards);
    }

    [Fact]
    public void IgnoresUnrelatedTwitchPropertyChanges()
    {
        var coordinator = new FakeTwitchSessionCoordinator(CreateState(CreateRewards(1)));
        var service = CreateService(coordinator);
        var facade = new TwitchRewardsFacade(service);
        var stateChangedCount = 0;
        facade.StateChanged += (_, _) => stateChangedCount++;

        facade.Initialize();
        RaisePropertyChanged(service, nameof(TwitchService.State));

        Assert.Equal(0, stateChangedCount);
        Assert.Single(facade.Rewards);
    }

    private static TwitchService CreateService(FakeTwitchSessionCoordinator coordinator) =>
        new(coordinator, NullLogger<TwitchService>.Instance, new NoOpDiagnosticsService());

    private static TwitchSessionState CreateState(IReadOnlyList<TwitchRewardSnapshot> rewards) =>
        new(
            TwitchSessionStatus.Ready,
            new TwitchAccountSession(
                TwitchAccountRole.Broadcaster,
                true,
                true,
                true,
                new TwitchIdentitySnapshot("broadcaster-id", "broadcaster", "Broadcaster", null)),
            null,
            TwitchAccountRole.Broadcaster,
            rewards,
            null);

    private static IReadOnlyList<TwitchRewardSnapshot> CreateRewards(int count) =>
        Enumerable.Range(0, count).Select(_ => CreateReward()).ToList();

    private static TwitchRewardSnapshot CreateReward() => new("reward-id", "Reward", "Prompt", 100, true);

    private static void SetRewards(TwitchService service, IReadOnlyList<TwitchRewardSnapshot> rewards)
    {
        var setter = typeof(TwitchService)
            .GetProperty(nameof(TwitchService.Rewards), BindingFlags.Instance | BindingFlags.Public)!
            .GetSetMethod(true)!;

        setter.Invoke(service, [rewards]);
    }

    private static void RaisePropertyChanged(TwitchService service, string propertyName)
    {
        var eventField = typeof(TwitchService)
            .GetField("PropertyChanged", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("PropertyChanged backing field was not found.");

        var handler = (PropertyChangedEventHandler?)eventField.GetValue(service);
        handler?.Invoke(service, new PropertyChangedEventArgs(propertyName));
    }

    private sealed class FakeTwitchSessionCoordinator : ITwitchSessionCoordinator
    {
        private readonly TaskCompletionSource _refreshStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _refreshGate = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public FakeTwitchSessionCoordinator(TwitchSessionState initialState)
        {
            CurrentState = initialState;
        }

        public TwitchSessionState CurrentState { get; private set; }
        public event EventHandler<TwitchSessionState>? StateChanged;
        public event EventHandler<TwitchSessionFailure>? SessionFaulted
        {
            add { }
            remove { }
        }

        public int RefreshRewardsCallCount { get; private set; }
        public TwitchSessionState? NextRefreshState { get; set; }

        public Task RefreshStarted => _refreshStarted.Task;

        public void AllowRefresh() => _refreshGate.TrySetResult();

        public Task InitializeIfNeededAsync() => Task.CompletedTask;

        public Task<TwitchSessionFailure?> StartBroadcasterAuthAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<TwitchSessionFailure?>(null);

        public Task<TwitchSessionFailure?> StartBotAuthAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<TwitchSessionFailure?>(null);

        public Task DisconnectBotAsync() => Task.CompletedTask;

        public Task LogoutAsync() => Task.CompletedTask;

        public async Task RefreshRewardsAsync()
        {
            RefreshRewardsCallCount++;
            _refreshStarted.TrySetResult();
            await _refreshGate.Task;

            if (NextRefreshState is not null)
            {
                SetState(NextRefreshState);
            }
        }

        public Task<ManagedRewardSyncResult> RefreshManagedRewardBindingAsync(
            CommandRewardBindingSnapshot binding,
            bool commandRequiresInput,
            CancellationToken cancellationToken = default) =>
            Task.FromException<ManagedRewardSyncResult>(new NotSupportedException());

        public Task<ManagedRewardSyncResult> CreateManagedRewardAsync(
            CommandRewardBindingSnapshot binding,
            bool commandRequiresInput,
            CancellationToken cancellationToken = default) =>
            Task.FromException<ManagedRewardSyncResult>(new NotSupportedException());

        public Task<ManagedRewardSyncResult> UpdateManagedRewardAsync(
            CommandRewardBindingSnapshot binding,
            bool commandRequiresInput,
            CancellationToken cancellationToken = default) =>
            Task.FromException<ManagedRewardSyncResult>(new NotSupportedException());

        private void SetState(TwitchSessionState state)
        {
            CurrentState = state;
            StateChanged?.Invoke(this, state);
        }
    }

    private sealed class NoOpDiagnosticsService : IAppDiagnosticsService
    {
        public event EventHandler<AppDiagnostic>? DiagnosticRecorded
        {
            add { }
            remove { }
        }
        public IReadOnlyList<AppDiagnostic> RecentDiagnostics => [];
        public Task ClearAsync() => Task.CompletedTask;

        public AppDiagnostic Record(
            AppDiagnosticSubsystem subsystem,
            AppDiagnosticSeverity severity,
            AppDiagnosticCategory category,
            string summary,
            string? detail = null,
            Exception? exception = null,
            AppDiagnosticVisibility visibility = AppDiagnosticVisibility.DiagnosticsOnly) =>
            new(
                Guid.NewGuid(),
                subsystem,
                severity,
                category,
                summary,
                detail,
                exception,
                exception?.ToString(),
                DateTimeOffset.UtcNow,
                visibility);
    }
}
