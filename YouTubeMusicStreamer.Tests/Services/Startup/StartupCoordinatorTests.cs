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

using Microsoft.Extensions.Logging.Abstractions;
using NuGet.Versioning;
using YouTubeMusicStreamer.Services.App;
using YouTubeMusicStreamer.Services.Startup;
using YouTubeMusicStreamer.Tests.TestSupport;

namespace YouTubeMusicStreamer.Tests.Services.Startup;

public sealed class StartupCoordinatorTests
{
    [Fact]
    public async Task OnWindowCreated_CompletesStartup_AndAutostartsWidgetServer_WhenEnabled()
    {
        var diagnostics = new RecordingDiagnosticsService();
        var runtime = new FakeStartupRuntimeServices
        {
            ShouldAutoStartWidgetServer = true,
        };
        runtime.Commands.Add(new StartupCommandDescriptor("!request", "Request song", true));
        var platform = new FakeStartupPlatform { WindowReady = true };
        var stateStore = new RecordingStartupStateStore();
        var delay = new FakeAsyncDelay();

        using var coordinator = CreateCoordinator(diagnostics, runtime, platform, stateStore, delay);

        coordinator.InitializePrimaryInstanceInfrastructure();
        Assert.Equal(1, platform.ActivatedSubscriberCount);
        coordinator.OnWindowCreated(null);

        var health = await WaitForHealthAsync(coordinator, h => h.State == InstanceRuntimeState.Ready);
        await WaitForConditionAsync(() => runtime.StartWidgetServerCallCount == 1);

        Assert.Equal(StartupPhase.Ready, health.Phase);
        Assert.Equal(1, runtime.InitializeSettingsCallCount);
        Assert.Equal(1, runtime.PreloadSensitiveSettingsCallCount);
        Assert.Equal(1, runtime.InitializeCommandsCallCount);
        Assert.Equal(1, runtime.InitializeVersionCallCount);
        Assert.Equal(1, runtime.InitializeYouTubeCallCount);
        Assert.Equal(1, runtime.InitializeTwitchCallCount);
        Assert.Equal(1, runtime.StartWidgetServerCallCount);
        Assert.Empty(coordinator.CurrentIssues);
        Assert.Contains(stateStore.PersistedHealth, h => h.Phase == StartupPhase.Ready);
    }

    [Fact]
    public async Task OnWindowCreated_SetsUnhealthyAndRecordsFatalIssue_WhenSettingsInitializationFails()
    {
        var diagnostics = new RecordingDiagnosticsService();
        var runtime = new FakeStartupRuntimeServices
        {
            InitializeSettingsException = new InvalidOperationException("db explode")
        };
        var platform = new FakeStartupPlatform { WindowReady = true };

        using var coordinator = CreateCoordinator(diagnostics, runtime, platform);

        coordinator.InitializePrimaryInstanceInfrastructure();
        coordinator.OnWindowCreated(null);

        var health = await WaitForHealthAsync(coordinator, h => h.State == InstanceRuntimeState.Unhealthy);

        Assert.Equal(StartupPhase.Failed, health.Phase);
        var issue = Assert.Single(coordinator.CurrentIssues);
        Assert.Equal(StartupIssueSeverity.Fatal, issue.Severity);
        Assert.Equal("Startup", issue.Source);
        Assert.Equal("db explode", issue.Detail);

        var diagnostic = Assert.Single(diagnostics.RecentDiagnostics);
        Assert.Equal("Fatal startup failure", diagnostic.Summary);
    }

    [Fact]
    public async Task OnWindowCreated_StaysReadyButRecordsIssues_WhenBackgroundSubsystemsFail()
    {
        var diagnostics = new RecordingDiagnosticsService();
        var runtime = new FakeStartupRuntimeServices
        {
            LastVersionCheckError = new InvalidOperationException("update unavailable"),
            InitializeYouTubeException = new InvalidOperationException("yt failed"),
            InitializeTwitchException = new InvalidOperationException("twitch failed")
        };
        var platform = new FakeStartupPlatform { WindowReady = true };

        using var coordinator = CreateCoordinator(diagnostics, runtime, platform);

        coordinator.InitializePrimaryInstanceInfrastructure();
        coordinator.OnWindowCreated(null);

        var health = await WaitForHealthAsync(coordinator, h => h.State == InstanceRuntimeState.Ready);

        Assert.Equal(StartupPhase.Ready, health.Phase);
        Assert.Contains(coordinator.CurrentIssues, issue => issue.Source == "Update check" && issue.Severity == StartupIssueSeverity.Warning);
        Assert.Contains(coordinator.CurrentIssues, issue => issue.Source == "YTMDesktop bootstrap" && issue.Severity == StartupIssueSeverity.Degraded);
        Assert.Contains(coordinator.CurrentIssues, issue => issue.Source == "Twitch restore" && issue.Severity == StartupIssueSeverity.Degraded);
        Assert.Equal(2, diagnostics.RecentDiagnostics.Count);
    }

    [Fact]
    public async Task ActivationBeforeWindowReady_IsQueuedAndBringsWindowToFront_WhenWindowBecomesReady()
    {
        var diagnostics = new RecordingDiagnosticsService();
        var runtime = new FakeStartupRuntimeServices();
        var platform = new FakeStartupPlatform { WindowReady = false };
        var delay = new FakeAsyncDelay();

        using var coordinator = CreateCoordinator(diagnostics, runtime, platform, delay: delay);

        coordinator.InitializePrimaryInstanceInfrastructure();
        coordinator.OnWindowCreated(null);

        await WaitForConditionAsync(() => delay.PendingDelayCount(TimeSpan.FromMilliseconds(50)) > 0);
        platform.RaiseActivated();
        platform.WindowReady = true;
        delay.ReleaseNext(TimeSpan.FromMilliseconds(50));

        await WaitForHealthAsync(coordinator, h => h.State == InstanceRuntimeState.Ready);

        Assert.Equal(1, platform.BringWindowToFrontCallCount);
    }

    [Fact]
    public async Task InitializePrimaryInstanceInfrastructure_InvalidPipePayload_ReturnsFailureResponse()
    {
        var commandServer = new FakeInstanceCommandServer();
        var factory = new FakeInstanceCommandServerFactory(commandServer);
        using var coordinator = CreateCoordinator(
            new RecordingDiagnosticsService(),
            new FakeStartupRuntimeServices(),
            new FakeStartupPlatform(),
            commandServerFactory: factory);

        coordinator.InitializePrimaryInstanceInfrastructure();

        commandServer.EnqueueCommand(null);
        var response = await commandServer.WaitForResultAsync();

        Assert.False(response.Success);
        Assert.Equal("Invalid command payload", response.Message);
    }

    [Fact]
    public async Task InitializePrimaryInstanceInfrastructure_ShowWindowCommandIsRejectedDuringShutdown()
    {
        var commandServer = new FakeInstanceCommandServer();
        var factory = new FakeInstanceCommandServerFactory(commandServer);
        using var coordinator = CreateCoordinator(
            new RecordingDiagnosticsService(),
            new FakeStartupRuntimeServices(),
            new FakeStartupPlatform(),
            commandServerFactory: factory);

        coordinator.InitializePrimaryInstanceInfrastructure();
        coordinator.OnWindowDestroying();

        commandServer.EnqueueCommand(new InstanceCommand(InstanceCommandType.ShowWindow));
        var response = await commandServer.WaitForResultAsync();

        Assert.False(response.Success);
        Assert.Equal("ShowWindow rejected during shutdown", response.Message);
    }

    [Fact]
    public void OnWindowDestroying_PersistsShuttingDownHealth()
    {
        var stateStore = new RecordingStartupStateStore();
        var platform = new FakeStartupPlatform();

        using var coordinator = CreateCoordinator(
            new RecordingDiagnosticsService(),
            new FakeStartupRuntimeServices(),
            platform,
            stateStore);

        coordinator.InitializePrimaryInstanceInfrastructure();
        coordinator.OnWindowDestroying();

        Assert.Equal(InstanceRuntimeState.ShuttingDown, coordinator.CurrentHealth.State);
        Assert.Equal(InstanceRuntimeState.ShuttingDown, stateStore.PersistedHealth[^1].State);
    }

    private static StartupCoordinator CreateCoordinator(
        RecordingDiagnosticsService diagnostics,
        FakeStartupRuntimeServices runtime,
        FakeStartupPlatform platform,
        RecordingStartupStateStore? stateStore = null,
        FakeAsyncDelay? delay = null,
        FakeInstanceCommandServerFactory? commandServerFactory = null)
    {
        return new StartupCoordinator(
            NullLogger<StartupCoordinator>.Instance,
            diagnostics,
            new FakeAppVersionSource(),
            new TestAppPathProvider(),
            runtime,
            platform,
            commandServerFactory ?? new FakeInstanceCommandServerFactory(),
            stateStore ?? new RecordingStartupStateStore(),
            delay ?? FakeAsyncDelay.Immediate);
    }

    private static async Task<InstanceHealthState> WaitForHealthAsync(StartupCoordinator coordinator, Func<InstanceHealthState, bool> predicate)
    {
        var tcs = new TaskCompletionSource<InstanceHealthState>(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<InstanceHealthState>? handler = null;
        handler = (_, state) =>
        {
            if (predicate(state))
                tcs.TrySetResult(state);
        };

        coordinator.HealthChanged += handler;
        try
        {
            if (predicate(coordinator.CurrentHealth))
                return coordinator.CurrentHealth;

            return await tcs.Task.WaitAsync(TimeSpan.FromSeconds(3));
        }
        finally
        {
            coordinator.HealthChanged -= handler;
        }
    }

    private static async Task WaitForConditionAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (condition())
                return;

            await Task.Delay(10);
        }

        throw new TimeoutException("Condition was not met within the expected test window.");
    }

    private sealed class FakeAppVersionSource : IAppVersionSource
    {
        public SemanticVersion GetInternalAppVersion() => new(1, 2, 3);
    }

    private sealed class FakeStartupRuntimeServices : IStartupRuntimeServices
    {
        public Exception? InitializeSettingsException { get; set; }
        public Exception? InitializeCommandsException { get; set; }
        public Exception? InitializeVersionException { get; set; }
        public Exception? InitializeYouTubeException { get; set; }
        public Exception? InitializeTwitchException { get; set; }
        public Exception? StartWidgetServerException { get; set; }
        public Exception? LastVersionCheckError { get; set; }
        public bool ShouldAutoStartWidgetServer { get; set; }
        public int InitializeSettingsCallCount { get; private set; }
        public int PreloadSensitiveSettingsCallCount { get; private set; }
        public int InitializeCommandsCallCount { get; private set; }
        public int InitializeVersionCallCount { get; private set; }
        public int InitializeYouTubeCallCount { get; private set; }
        public int InitializeTwitchCallCount { get; private set; }
        public int StartWidgetServerCallCount { get; private set; }
        public int StopWidgetServerCallCount { get; private set; }
        public List<StartupCommandDescriptor> Commands { get; } = [];
        Exception? IStartupRuntimeServices.LastVersionCheckError => LastVersionCheckError;

        public Task InitializeSettingsAsync()
        {
            InitializeSettingsCallCount++;
            return InitializeSettingsException is null ? Task.CompletedTask : Task.FromException(InitializeSettingsException);
        }

        public Task PreloadSensitiveSettingsAsync()
        {
            PreloadSensitiveSettingsCallCount++;
            return Task.CompletedTask;
        }

        public Task InitializeCommandsAsync()
        {
            InitializeCommandsCallCount++;
            return InitializeCommandsException is null ? Task.CompletedTask : Task.FromException(InitializeCommandsException);
        }

        public IReadOnlyList<StartupCommandDescriptor> ListCommands() => Commands;

        public Task InitializeVersionAsync()
        {
            InitializeVersionCallCount++;
            return InitializeVersionException is null ? Task.CompletedTask : Task.FromException(InitializeVersionException);
        }

        public Task InitializeYouTubeAsync()
        {
            InitializeYouTubeCallCount++;
            return InitializeYouTubeException is null ? Task.CompletedTask : Task.FromException(InitializeYouTubeException);
        }

        public Task InitializeTwitchAsync()
        {
            InitializeTwitchCallCount++;
            return InitializeTwitchException is null ? Task.CompletedTask : Task.FromException(InitializeTwitchException);
        }

        public Task StartWidgetServerAsync()
        {
            StartWidgetServerCallCount++;
            return StartWidgetServerException is null ? Task.CompletedTask : Task.FromException(StartWidgetServerException);
        }

        public void StopWidgetServer() => StopWidgetServerCallCount++;

        public void ValidateDevelopmentConfiguration()
        {
        }
    }

    private sealed class FakeStartupPlatform : IStartupPlatform
    {
        private EventHandler? _activated;
        private EventHandler? _processExit;

        public event EventHandler Activated
        {
            add
            {
                _activated += value;
                ActivatedSubscriberCount++;
            }
            remove
            {
                _activated -= value;
                ActivatedSubscriberCount--;
            }
        }

        public event EventHandler ProcessExit
        {
            add
            {
                _processExit += value;
                ProcessExitSubscriberCount++;
            }
            remove
            {
                _processExit -= value;
                ProcessExitSubscriberCount--;
            }
        }

        public bool WindowReady { get; set; }
        public int BringWindowToFrontCallCount { get; private set; }
        public int ActivatedSubscriberCount { get; private set; }
        public int ProcessExitSubscriberCount { get; private set; }
        public List<string> EnsuredDirectories { get; } = [];

        public void EnsureDirectoryExists(string path) => EnsuredDirectories.Add(path);

        public bool IsWindowReady(Window? window) => WindowReady;

        public void BringWindowToFront(Window? window) => BringWindowToFrontCallCount++;

        public void RaiseActivated() => _activated?.Invoke(this, EventArgs.Empty);
        public void RaiseProcessExit() => _processExit?.Invoke(this, EventArgs.Empty);
    }

    private sealed class FakeInstanceCommandServerFactory(params FakeInstanceCommandServer[] servers) : IInstanceCommandServerFactory
    {
        private readonly Queue<FakeInstanceCommandServer> _servers = new(servers);

        public IInstanceCommandServer Create()
        {
            if (_servers.Count > 0)
                return _servers.Dequeue();

            return new FakeInstanceCommandServer();
        }
    }

    private sealed class FakeInstanceCommandServer : IInstanceCommandServer
    {
        private readonly TaskCompletionSource<InstanceCommand?> _commandTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<InstanceCommandResult> _resultTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void EnqueueCommand(InstanceCommand? command) => _commandTcs.TrySetResult(command);

        public async Task<InstanceCommand?> WaitForCommandAsync(CancellationToken cancellationToken) =>
            await _commandTcs.Task.WaitAsync(cancellationToken);

        public Task SendResultAsync(InstanceCommandResult result, CancellationToken cancellationToken)
        {
            _resultTcs.TrySetResult(result);
            return Task.CompletedTask;
        }

        public async Task<InstanceCommandResult> WaitForResultAsync() => await _resultTcs.Task.WaitAsync(TimeSpan.FromSeconds(3));

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class RecordingStartupStateStore : IStartupStateStore
    {
        public List<InstanceHealthState> PersistedHealth { get; } = [];

        public void Persist(InstanceHealthState health) => PersistedHealth.Add(health);
    }

    private sealed class FakeAsyncDelay : IAsyncDelay
    {
        private readonly bool _immediate;
        private readonly object _sync = new();
        private readonly Dictionary<TimeSpan, Queue<TaskCompletionSource<bool>>> _pending = [];

        public static FakeAsyncDelay Immediate { get; } = new(immediate: true);

        public FakeAsyncDelay(bool immediate = false)
        {
            _immediate = immediate;
        }

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default)
        {
            if (_immediate)
            {
                if (delay < TimeSpan.FromSeconds(1))
                    return Task.CompletedTask;

                return Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));

            lock (_sync)
            {
                if (!_pending.TryGetValue(delay, out var queue))
                {
                    queue = new Queue<TaskCompletionSource<bool>>();
                    _pending[delay] = queue;
                }

                queue.Enqueue(tcs);
            }
            return tcs.Task;
        }

        public int PendingDelayCount(TimeSpan delay)
        {
            lock (_sync)
            {
                return _pending.TryGetValue(delay, out var queue) ? queue.Count : 0;
            }
        }

        public void ReleaseNext(TimeSpan delay)
        {
            lock (_sync)
            {
                if (_pending.TryGetValue(delay, out var queue) && queue.Count > 0)
                    queue.Dequeue().TrySetResult(true);
            }
        }
    }
}
