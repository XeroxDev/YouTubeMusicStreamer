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

using Microsoft.Extensions.Logging;
using YouTubeMusicStreamer.Services.App;
using YouTubeMusicStreamer.Services.App.Diagnostics;

namespace YouTubeMusicStreamer.Services.Startup;

public sealed class StartupCoordinator : IStartupCoordinator, IDisposable
{
    private readonly ILogger<StartupCoordinator> _logger;
    private readonly IAppDiagnosticsService _diagnosticsService;
    private readonly IAppVersionSource _appVersionSource;
    private readonly IAppPathProvider _appPathProvider;
    private readonly IStartupRuntimeServices _runtimeServices;
    private readonly IStartupPlatform _startupPlatform;
    private readonly IInstanceCommandServerFactory _commandServerFactory;
    private readonly IStartupStateStore _stateStore;
    private readonly IAsyncDelay _delay;
    private readonly SemaphoreSlim _startupLock = new(1, 1);
    private readonly List<StartupIssue> _issues = [];

    private readonly string _launchToken = Guid.NewGuid().ToString("N");
    private readonly DateTime _startedAtUtc = DateTime.UtcNow;

    private CancellationTokenSource? _backgroundCts;
    private Task? _pipeServerTask;
    private Task? _heartbeatTask;
    private Task? _startupTask;
    private readonly List<Task> _managedBackgroundTasks = [];
    private Window? _window;
    private bool _pendingShowWindowRequest;
    private bool _infrastructureInitialized;
    private bool _isShuttingDown;
    public StartupCoordinator(
        ILogger<StartupCoordinator> logger,
        IAppDiagnosticsService diagnosticsService,
        IAppVersionSource appVersionSource,
        IAppPathProvider appPathProvider,
        IStartupRuntimeServices runtimeServices,
        IStartupPlatform startupPlatform,
        IInstanceCommandServerFactory commandServerFactory,
        IStartupStateStore stateStore,
        IAsyncDelay delay)
    {
        _logger = logger;
        _diagnosticsService = diagnosticsService;
        _appVersionSource = appVersionSource;
        _appPathProvider = appPathProvider;
        _runtimeServices = runtimeServices;
        _startupPlatform = startupPlatform;
        _commandServerFactory = commandServerFactory;
        _stateStore = stateStore;
        _delay = delay;

        CurrentHealth = CreateHealth(InstanceRuntimeState.Starting, StartupPhase.NotStarted);
    }

    public InstanceHealthState CurrentHealth { get; private set; }
    public IReadOnlyList<StartupIssue> CurrentIssues => _issues;
    public event EventHandler<InstanceHealthState> HealthChanged = delegate { };

    public void InitializePrimaryInstanceInfrastructure()
    {
        if (_infrastructureInitialized)
            return;

        _infrastructureInitialized = true;
        _startupPlatform.EnsureDirectoryExists(_appPathProvider.AppDataDirectory);

        _backgroundCts = new CancellationTokenSource();
        PersistHealth(CreateHealth(InstanceRuntimeState.Starting, StartupPhase.LoggingBootstrapReady));

        _startupPlatform.Activated += OnAppActivated;
        _startupPlatform.ProcessExit += OnProcessExit;

        _pipeServerTask = Task.Run(() => RunPipeServerAsync(_backgroundCts.Token));
        _heartbeatTask = Task.Run(() => RunHeartbeatAsync(_backgroundCts.Token));
    }

    public void OnWindowCreated(Window? window)
    {
        _window = window;
        _startupTask ??= Task.Run(RunStartupAsync);
    }

    public void OnWindowDestroying()
    {
        if (_isShuttingDown)
            return;

        _isShuttingDown = true;
        PersistHealth(CreateHealth(InstanceRuntimeState.ShuttingDown, CurrentHealth.Phase));
    }

    private async Task RunStartupAsync()
    {
        await _startupLock.WaitAsync();
        try
        {
            _logger.LogInformation("Starting coordinated startup");

            PersistHealth(CreateHealth(InstanceRuntimeState.Starting, StartupPhase.LoggingBootstrapReady));

#if DEBUG
            ValidateDevelopmentStartupConfiguration();
#endif
            await _runtimeServices.InitializeSettingsAsync();
            await _runtimeServices.PreloadSensitiveSettingsAsync();
            PersistHealth(CreateHealth(InstanceRuntimeState.Starting, StartupPhase.SettingsLoaded));

            await _runtimeServices.InitializeCommandsAsync();
            PersistHealth(CreateHealth(InstanceRuntimeState.Starting, StartupPhase.CoreServicesReady));

            foreach (var command in _runtimeServices.ListCommands())
            {
                _logger.LogInformation("Found command: {Trigger} - {Description} | Enabled: {IsEnabled}", command.Trigger, command.Description, command.IsEnabled);
            }

            await WaitForWindowReadyAsync();
            PersistHealth(CreateHealth(InstanceRuntimeState.Starting, StartupPhase.WindowReady));

            await StartBackgroundSubsystemsAsync();
            PersistHealth(CreateHealth(InstanceRuntimeState.Starting, StartupPhase.BackgroundSubsystemsStarted));

            PersistHealth(CreateHealth(InstanceRuntimeState.Ready, StartupPhase.Ready));
            _logger.LogInformation("Startup coordinator reached ready state");
        }
        catch (Exception ex)
        {
            RecordIssue(StartupIssueSeverity.Fatal, "Startup", "Fatal startup failure", ex.Message);
            _logger.Diagnostic(_diagnosticsService, AppDiagnosticSubsystem.Startup)
                .Error(AppDiagnosticCategory.Startup, "Fatal startup failure")
                .WithDetail(ex.Message)
                .WithException(ex)
                .Toast()
                .Write();
            PersistHealth(CreateHealth(InstanceRuntimeState.Unhealthy, StartupPhase.Failed));
        }
        finally
        {
            _startupLock.Release();
        }
    }

    private async Task WaitForWindowReadyAsync()
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (_startupPlatform.IsWindowReady(_window))
            {
                if (_pendingShowWindowRequest)
                {
                    _pendingShowWindowRequest = false;
                    BringWindowToFront();
                }

                return;
            }

            await _delay.DelayAsync(TimeSpan.FromMilliseconds(50));
        }

        throw new InvalidOperationException("Primary window was not ready within the expected startup window.");
    }

    private async Task StartBackgroundSubsystemsAsync()
    {
        await RunBackgroundSubsystemAsync("Update check", async () =>
        {
            await _runtimeServices.InitializeVersionAsync();
            if (_runtimeServices.LastVersionCheckError is not null)
            {
                RecordIssue(
                    StartupIssueSeverity.Warning,
                    "Update check",
                    "Update check failed during startup",
                    _runtimeServices.LastVersionCheckError.Message);
            }
        });
        await RunBackgroundSubsystemAsync("YTMDesktop bootstrap", () => _runtimeServices.InitializeYouTubeAsync());
        await RunBackgroundSubsystemAsync("Twitch restore", () => _runtimeServices.InitializeTwitchAsync());

        if (_runtimeServices.ShouldAutoStartWidgetServer)
        {
            StartManagedBackgroundTask("Widget server autostart", () => _runtimeServices.StartWidgetServerAsync());
        }
    }

    private async Task RunBackgroundSubsystemAsync(string name, Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            RecordIssue(StartupIssueSeverity.Degraded, name, $"Background startup subsystem failed: {name}", ex.Message);
            _logger.Diagnostic(_diagnosticsService, AppDiagnosticSubsystem.Startup)
                .Warning(AppDiagnosticCategory.Startup, $"Background startup subsystem failed: {name}")
                .WithDetail(ex.Message)
                .WithException(ex)
                .StatusOnly()
                .Write();
        }
    }

    private void OnAppActivated(object? sender, EventArgs args)
    {
        try
        {
            RequestShowWindow();
        }
        catch (Exception ex)
        {
            _logger.Diagnostic(_diagnosticsService, AppDiagnosticSubsystem.Startup)
                .Warning(AppDiagnosticCategory.Startup, "Failed to handle app activation")
                .WithLogLevel(LogLevel.Error)
                .WithDetail(ex.Message)
                .WithException(ex)
                .Write();
        }
    }

    private void RequestShowWindow()
    {
        if (_isShuttingDown)
            return;

        if (!_startupPlatform.IsWindowReady(_window))
        {
            _pendingShowWindowRequest = true;
            return;
        }

        BringWindowToFront();
    }

    private void BringWindowToFront()
    {
        if (!_startupPlatform.IsWindowReady(_window))
        {
            _pendingShowWindowRequest = true;
            return;
        }

        _startupPlatform.BringWindowToFront(_window);
    }

    private async Task RunPipeServerAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var server = _commandServerFactory.Create();
                var command = await server.WaitForCommandAsync(cancellationToken);

                var result = command is null
                    ? new InstanceCommandResult(false, "Invalid command payload", CurrentHealth)
                    : await HandleCommandAsync(command);

                await server.SendResultAsync(result, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.Diagnostic(_diagnosticsService, AppDiagnosticSubsystem.Startup)
                    .Warning(AppDiagnosticCategory.Startup, "Primary-instance pipe loop iteration failed")
                    .WithDetail(ex.Message)
                    .WithException(ex)
                    .Write();
            }
        }
    }

    private Task<InstanceCommandResult> HandleCommandAsync(InstanceCommand command)
    {
        var result = command.Type switch
        {
            InstanceCommandType.Ping => new InstanceCommandResult(true, _isShuttingDown ? "Shutting down" : "Pong", CurrentHealth),
            InstanceCommandType.GetHealth => new InstanceCommandResult(true, _isShuttingDown ? "Current health (shutting down)" : "Current health", CurrentHealth),
            InstanceCommandType.ShowWindow => HandleShowWindowCommand(),
            InstanceCommandType.ShutdownIntent => new InstanceCommandResult(true, _isShuttingDown ? "Shutdown in progress" : "Not shutting down", CurrentHealth),
            _ => new InstanceCommandResult(false, "Unsupported command", CurrentHealth)
        };

        return Task.FromResult(result);
    }

    private InstanceCommandResult HandleShowWindowCommand()
    {
        if (_isShuttingDown)
            return new InstanceCommandResult(false, "ShowWindow rejected during shutdown", CurrentHealth);

        RequestShowWindow();
        var message = CurrentHealth.State == InstanceRuntimeState.Ready
            ? "ShowWindow accepted"
            : "ShowWindow queued until startup completes";

        return new InstanceCommandResult(true, message, CurrentHealth);
    }

    private async Task RunHeartbeatAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await _delay.DelayAsync(TimeSpan.FromSeconds(5), cancellationToken);
                PersistHealth(CurrentHealth with { LastUpdatedUtc = DateTime.UtcNow });
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void OnProcessExit(object? sender, EventArgs args)
    {
        try
        {
            PersistHealth(CreateHealth(InstanceRuntimeState.ShuttingDown, CurrentHealth.Phase));
        }
        catch
        {
            // ignored during shutdown
        }
    }

    private InstanceHealthState CreateHealth(InstanceRuntimeState state, StartupPhase phase) => new(
        Environment.ProcessId,
        _launchToken,
        _startedAtUtc,
        DateTime.UtcNow,
        state,
        phase,
        _appVersionSource.GetInternalAppVersion().ToString());

    private void PersistHealth(InstanceHealthState health)
    {
        CurrentHealth = health;
        _stateStore.Persist(health);
        HealthChanged(this, CurrentHealth);
    }

    private void ValidateDevelopmentStartupConfiguration()
    {
        try
        {
            _runtimeServices.ValidateDevelopmentConfiguration();
        }
        catch (Exception ex)
        {
            RecordIssue(StartupIssueSeverity.Fatal, "DI", "Development-time service validation failed", ex.Message);
            _logger.Diagnostic(_diagnosticsService, AppDiagnosticSubsystem.Startup)
                .Error(AppDiagnosticCategory.Startup, "Development-time service validation failed")
                .WithDetail(ex.Message)
                .WithException(ex)
                .Write();
            throw;
        }
    }

    private void StartManagedBackgroundTask(string name, Func<Task> taskFactory)
    {
        var task = Task.Run(async () =>
        {
            try
            {
                await taskFactory();
            }
            catch (Exception ex)
            {
                RecordIssue(StartupIssueSeverity.Degraded, name, $"{name} failed after startup", ex.Message);
                _logger.Diagnostic(_diagnosticsService, AppDiagnosticSubsystem.Startup)
                    .Warning(AppDiagnosticCategory.Startup, $"{name} failed after startup")
                    .WithLogLevel(LogLevel.Error)
                    .WithDetail(ex.Message)
                    .WithException(ex)
                    .StatusOnly()
                    .Write();
            }
        });

        _managedBackgroundTasks.Add(task);
    }

    private void RecordIssue(StartupIssueSeverity severity, string source, string message, string? detail = null)
    {
        _issues.Add(new StartupIssue(severity, source, message, DateTime.UtcNow, detail));
    }

    public void Dispose()
    {
        _startupPlatform.Activated -= OnAppActivated;
        _startupPlatform.ProcessExit -= OnProcessExit;

        if (_backgroundCts is null)
            return;

        PersistHealth(CreateHealth(InstanceRuntimeState.ShuttingDown, CurrentHealth.Phase));
        try
        {
            _runtimeServices.StopWidgetServer();
        }
        catch (Exception ex)
        {
            _logger.Diagnostic(_diagnosticsService, AppDiagnosticSubsystem.Startup)
                .Warning(AppDiagnosticCategory.Startup, "Failed to stop widget server during shutdown")
                .WithDetail(ex.Message)
                .WithException(ex)
                .Write();
        }

        _backgroundCts.Cancel();
        _backgroundCts.Dispose();
        _backgroundCts = null;
    }
}
