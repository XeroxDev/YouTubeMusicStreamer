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
using Microsoft.AspNetCore.Components;
using YouTubeMusicStreamer.Services;
using YouTubeMusicStreamer.Services.App;
using YouTubeMusicStreamer.Services.Startup;
using YouTubeMusicStreamer.Services.Twitch;
using TwitchService = YouTubeMusicStreamer.Services.Twitch.TwitchService;

namespace YouTubeMusicStreamer.Components.Pages.Home.Components;

public partial class TwitchConnection : ComponentBase, IDisposable
{
    [Inject] private TwitchService TwitchService { get; set; } = null!;
    [Inject] private IAppToastService ToastService { get; set; } = null!;
    [Inject] private IStartupCoordinator StartupCoordinator { get; set; } = null!;

    protected override void OnInitialized()
    {
        TwitchService.PropertyChanged += OnTwitchServiceOnPropertyChanged;
        StartupCoordinator.HealthChanged += OnStartupHealthChanged;
    }

    public void Dispose()
    {
        TwitchService.PropertyChanged -= OnTwitchServiceOnPropertyChanged;
        StartupCoordinator.HealthChanged -= OnStartupHealthChanged;
        GC.SuppressFinalize(this);
    }

    private void OnTwitchServiceOnPropertyChanged(object? o, PropertyChangedEventArgs propertyChangedEventArgs) =>
        _ = InvokeAsync(StateHasChanged);

    private void OnStartupHealthChanged(object? sender, InstanceHealthState health) =>
        _ = InvokeAsync(StateHasChanged);

    private async Task StartOAuthFlow() => await TwitchService.StartOAuthFlowAsync(void (error) => ToastService.ShowError(error));
    private async Task StartBotOAuthFlow() => await TwitchService.StartBotOAuthFlowAsync(void (error) => ToastService.ShowError(error));
    private Task CancelAuth()
    {
        TwitchService.CancelPendingAuth();
        return Task.CompletedTask;
    }

    private async Task Logout() => await TwitchService.LogoutAsync();
    private async Task DisconnectBot() => await TwitchService.DisconnectBotAsync();

    private string EffectiveChatIdentitySuffix =>
        TwitchConnectionViewState.GetEffectiveChatIdentitySuffix(
            TwitchService.EffectiveChatRole,
            TwitchService.BotUsername,
            TwitchService.Username);

    private bool IsStartupRestoringTwitch =>
        TwitchConnectionViewState.IsStartupRestoringTwitch(StartupCoordinator.CurrentHealth);
}
