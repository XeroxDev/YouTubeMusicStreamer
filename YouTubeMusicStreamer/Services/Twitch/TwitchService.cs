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
using YouTubeMusicStreamer.Enums;
using YouTubeMusicStreamer.Services.App;
using YouTubeMusicStreamer.Services.App.Diagnostics;
using YouTubeMusicStreamer.Services.App.Persistence;
using YouTubeMusicStreamer.Services.Twitch.Interfaces;

namespace YouTubeMusicStreamer.Services.Twitch;

public sealed partial class TwitchService : ITwitchStatusSource, IDisposable
{
    private ConnectorState _state;
    private TwitchSessionStatus _sessionStatus;
    private string? _username;
    private string? _broadcasterAccountId;
    private string? _botUsername;
    private string? _profileImageUrl;
    private string? _botProfileImageUrl;
    private List<TwitchRewardSnapshot> _rewards = [];
    private TwitchAccountRole _effectiveChatRole;
    private bool _isAuthInProgress;
    private TwitchAccountRole? _pendingAuthRole;
    private CancellationTokenSource? _authCancellation;

    public ConnectorState State
    {
        get => _state;
        private set => SetField(ref _state, value);
    }

    public TwitchSessionStatus SessionStatus
    {
        get => _sessionStatus;
        private set => SetField(ref _sessionStatus, value);
    }

    public string? Username
    {
        get => _username;
        private set => SetField(ref _username, value);
    }

    public string? BroadcasterAccountId
    {
        get => _broadcasterAccountId;
        private set => SetField(ref _broadcasterAccountId, value);
    }

    public string? BotUsername
    {
        get => _botUsername;
        private set => SetField(ref _botUsername, value);
    }

    public string? ProfileImageUrl
    {
        get => _profileImageUrl;
        private set => SetField(ref _profileImageUrl, value);
    }

    public string? BotProfileImageUrl
    {
        get => _botProfileImageUrl;
        private set => SetField(ref _botProfileImageUrl, value);
    }

    public IReadOnlyList<TwitchRewardSnapshot> Rewards
    {
        get => _rewards;
        private set => SetField(ref _rewards, value.ToList());
    }

    public TwitchAccountRole EffectiveChatRole
    {
        get => _effectiveChatRole;
        private set => SetField(ref _effectiveChatRole, value);
    }

    public bool IsAuthInProgress
    {
        get => _isAuthInProgress;
        private set => SetField(ref _isAuthInProgress, value);
    }

    public TwitchAccountRole? PendingAuthRole
    {
        get => _pendingAuthRole;
        private set => SetField(ref _pendingAuthRole, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private readonly ITwitchSessionCoordinator _sessionCoordinator;
    private readonly ILogger<TwitchService> _logger;
    private readonly IAppDiagnosticsService _diagnosticsService;
    private bool _disposed;

    public TwitchService(
        ITwitchSessionCoordinator sessionCoordinator,
        ILogger<TwitchService> logger,
        IAppDiagnosticsService diagnosticsService)
    {
        _sessionCoordinator = sessionCoordinator;
        _logger = logger;
        _diagnosticsService = diagnosticsService;

        _sessionCoordinator.StateChanged += OnStateChanged;
        _sessionCoordinator.SessionFaulted += OnSessionFaulted;
        State = ConnectorState.LoggedOut;
        SessionStatus = TwitchSessionStatus.LoggedOut;
        ApplySessionState(_sessionCoordinator.CurrentState);
    }

    public async Task InitializeIfNeededAsync()
    {
        if (!IsAuthInProgress)
        {
            State = ConnectorState.Loading;
        }

        await _sessionCoordinator.InitializeIfNeededAsync();
    }

    public async Task StartOAuthFlowAsync(Action<string> onError)
    {
        if (IsAuthInProgress)
            return;

        _logger.LogInformation("Starting OAuth flow");
        BeginAuth(TwitchAccountRole.Broadcaster);

        try
        {
            var failure = await _sessionCoordinator.StartBroadcasterAuthAsync(_authCancellation!.Token);
            if (failure is null)
                return;

            onError(failure.Message);
        }
        catch (OperationCanceledException) when (_authCancellation?.IsCancellationRequested == true)
        {
            _logger.LogInformation("Twitch broadcaster OAuth flow was canceled");
        }
        catch (Exception ex)
        {
            Diagnostic()
                .Error(AppDiagnosticCategory.Auth, "Twitch broadcaster authentication failed.")
                .WithDetail(ex.Message)
                .WithException(ex)
                .Toast()
                .Write();
            onError("Unexpected error during OAuth flow");
        }
        finally
        {
            EndAuth();
        }
    }

    public async Task RefreshRewardsAsync()
    {
        try
        {
            await _sessionCoordinator.RefreshRewardsAsync();
        }
        catch (Exception ex)
        {
            Diagnostic()
                .Warning(AppDiagnosticCategory.Configuration, "Failed to refresh Twitch rewards.")
                .WithLogLevel(LogLevel.Error)
                .WithDetail(ex.Message)
                .WithException(ex)
                .StatusOnly()
                .Write();
        }
    }

    public async Task<ManagedRewardSyncResult> RefreshManagedRewardBindingAsync(CommandRewardBindingSnapshot binding, bool commandRequiresInput)
    {
        try
        {
            return await _sessionCoordinator.RefreshManagedRewardBindingAsync(binding, commandRequiresInput);
        }
        catch (Exception ex)
        {
            Diagnostic()
                .Error(AppDiagnosticCategory.Configuration, "Failed to refresh managed reward binding.")
                .WithDetail(ex.Message)
                .WithException(ex)
                .Write();
            throw;
        }
    }

    public async Task<ManagedRewardSyncResult> CreateManagedRewardAsync(CommandRewardBindingSnapshot binding, bool commandRequiresInput)
    {
        try
        {
            return await _sessionCoordinator.CreateManagedRewardAsync(binding, commandRequiresInput);
        }
        catch (Exception ex)
        {
            Diagnostic()
                .Error(AppDiagnosticCategory.Configuration, "Failed to create managed reward.")
                .WithDetail(ex.Message)
                .WithException(ex)
                .Write();
            throw;
        }
    }

    public async Task<ManagedRewardSyncResult> UpdateManagedRewardAsync(CommandRewardBindingSnapshot binding, bool commandRequiresInput)
    {
        try
        {
            return await _sessionCoordinator.UpdateManagedRewardAsync(binding, commandRequiresInput);
        }
        catch (Exception ex)
        {
            Diagnostic()
                .Error(AppDiagnosticCategory.Configuration, "Failed to update managed reward.")
                .WithDetail(ex.Message)
                .WithException(ex)
                .Write();
            throw;
        }
    }

    public async Task LogoutAsync()
    {
        try
        {
            await _sessionCoordinator.LogoutAsync();
        }
        catch (Exception ex)
        {
            Diagnostic()
                .Error(AppDiagnosticCategory.Auth, "Failed to log out from Twitch.")
                .WithDetail(ex.Message)
                .WithException(ex)
                .Toast()
                .Write();
        }
    }

    public async Task StartBotOAuthFlowAsync(Action<string> onError)
    {
        if (IsAuthInProgress)
            return;

        BeginAuth(TwitchAccountRole.Bot);

        try
        {
            var failure = await _sessionCoordinator.StartBotAuthAsync(_authCancellation!.Token);
            if (failure is null)
                return;

            onError(failure.Message);
        }
        catch (OperationCanceledException) when (_authCancellation?.IsCancellationRequested == true)
        {
            _logger.LogInformation("Twitch bot OAuth flow was canceled");
        }
        catch (Exception ex)
        {
            Diagnostic()
                .Error(AppDiagnosticCategory.Auth, "Twitch bot authentication failed.")
                .WithDetail(ex.Message)
                .WithException(ex)
                .Toast()
                .Write();
            onError("Unexpected error during bot OAuth flow");
        }
        finally
        {
            EndAuth();
        }
    }

    public async Task DisconnectBotAsync()
    {
        try
        {
            await _sessionCoordinator.DisconnectBotAsync();
        }
        catch (Exception ex)
        {
            Diagnostic()
                .Error(AppDiagnosticCategory.Auth, "Failed to disconnect Twitch bot.")
                .WithDetail(ex.Message)
                .WithException(ex)
                .Toast()
                .Write();
        }
    }

    public void CancelPendingAuth()
    {
        if (!IsAuthInProgress)
            return;

        _authCancellation?.Cancel();
    }

    private void OnStateChanged(object? sender, TwitchSessionState state) => ApplySessionState(state);

    private void OnSessionFaulted(object? sender, TwitchSessionFailure failure)
    {
        if (failure.Kind == TwitchFailureKind.AuthCancelled)
            return;

        RecordDiagnostic(
            failure.Kind is TwitchFailureKind.Unknown or TwitchFailureKind.AccountReset
                ? AppDiagnosticSeverity.Error
                : AppDiagnosticSeverity.Warning,
            MapDiagnosticCategory(failure.Kind),
            failure.Message,
            failure.Exception?.Message,
            failure.Exception,
            MapDiagnosticVisibility(failure.Kind));
    }

    private void ApplySessionState(TwitchSessionState state)
    {
        SessionStatus = state.Status;
        BroadcasterAccountId = state.Broadcaster.Identity?.UserId;
        Username = state.Broadcaster.Identity?.DisplayName;
        BotUsername = state.Bot?.Identity?.DisplayName;
        ProfileImageUrl = state.Broadcaster.Identity?.ProfileImageUrl;
        BotProfileImageUrl = state.Bot?.Identity?.ProfileImageUrl;
        Rewards = state.Rewards;
        EffectiveChatRole = state.EffectiveChatRole;
        State = IsAuthInProgress ? ConnectorState.LoggingIn : MapConnectorState(state.Status);
    }

    private void BeginAuth(TwitchAccountRole role)
    {
        EndAuth();
        _authCancellation = new CancellationTokenSource();
        PendingAuthRole = role;
        IsAuthInProgress = true;
        State = ConnectorState.LoggingIn;
    }

    private void EndAuth()
    {
        _authCancellation?.Dispose();
        _authCancellation = null;
        PendingAuthRole = null;
        IsAuthInProgress = false;
        State = MapConnectorState(SessionStatus);
    }

    private static ConnectorState MapConnectorState(TwitchSessionStatus status) => status switch
    {
        TwitchSessionStatus.NotConfigured => ConnectorState.LoggedOut,
        TwitchSessionStatus.AuthRequired => ConnectorState.LoggedOut,
        TwitchSessionStatus.LoggedOut => ConnectorState.LoggedOut,
        TwitchSessionStatus.ValidatingBroadcaster => ConnectorState.Loading,
        TwitchSessionStatus.ValidatingBot => ConnectorState.Loading,
        TwitchSessionStatus.BroadcasterReady => ConnectorState.Loading,
        TwitchSessionStatus.ConnectingChat => ConnectorState.Loading,
        TwitchSessionStatus.ConnectingEventSub => ConnectorState.Loading,
        TwitchSessionStatus.SwitchingAccount => ConnectorState.Loading,
        TwitchSessionStatus.Ready => ConnectorState.LoggedIn,
        TwitchSessionStatus.Degraded => ConnectorState.LoggedIn,
        TwitchSessionStatus.Error => ConnectorState.Error,
        _ => ConnectorState.Error
    };

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
        if (_disposed)
            return;

        _disposed = true;
        _authCancellation?.Cancel();
        _authCancellation?.Dispose();
        _sessionCoordinator.StateChanged -= OnStateChanged;
        _sessionCoordinator.SessionFaulted -= OnSessionFaulted;
        GC.SuppressFinalize(this);
    }

    private void RecordDiagnostic(
        AppDiagnosticSeverity severity,
        AppDiagnosticCategory category,
        string summary,
        string? detail = null,
        Exception? exception = null,
        AppDiagnosticVisibility visibility = AppDiagnosticVisibility.DiagnosticsOnly)
    {
        _diagnosticsService.Record(
            AppDiagnosticSubsystem.Twitch,
            severity,
            category,
            summary,
            detail,
            exception,
            visibility);
    }

    private DiagnosticReportBuilder<TwitchService> Diagnostic() => _logger.Diagnostic(_diagnosticsService, AppDiagnosticSubsystem.Twitch);

    private static AppDiagnosticCategory MapDiagnosticCategory(TwitchFailureKind kind) => kind switch
    {
        TwitchFailureKind.AuthValidation or TwitchFailureKind.AuthCancelled or TwitchFailureKind.CallbackListenerBind => AppDiagnosticCategory.Auth,
        TwitchFailureKind.IdentityBootstrap or TwitchFailureKind.AccountReset => AppDiagnosticCategory.Persistence,
        TwitchFailureKind.ChatTransport or TwitchFailureKind.EventSubTransport or TwitchFailureKind.EventSubSubscription => AppDiagnosticCategory.Connectivity,
        TwitchFailureKind.RewardLoad => AppDiagnosticCategory.Configuration,
        _ => AppDiagnosticCategory.InternalFault
    };

    private static AppDiagnosticVisibility MapDiagnosticVisibility(TwitchFailureKind kind) => kind switch
    {
        TwitchFailureKind.AuthValidation or TwitchFailureKind.RewardLoad or TwitchFailureKind.ChatTransport or TwitchFailureKind.EventSubTransport or TwitchFailureKind.EventSubSubscription => AppDiagnosticVisibility.StatusOnly,
        TwitchFailureKind.AccountReset or TwitchFailureKind.Unknown => AppDiagnosticVisibility.Toast,
        _ => AppDiagnosticVisibility.DiagnosticsOnly
    };
}
