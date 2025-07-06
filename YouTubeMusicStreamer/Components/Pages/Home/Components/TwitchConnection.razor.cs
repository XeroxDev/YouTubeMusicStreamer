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
using Blazored.Toast.Services;
using Microsoft.AspNetCore.Components;
using YouTubeMusicStreamer.Services;
using TwitchService = YouTubeMusicStreamer.Services.Twitch.TwitchService;

namespace YouTubeMusicStreamer.Components.Pages.Home.Components;

public partial class TwitchConnection : ComponentBase, IDisposable
{
    [Inject] private TwitchService TwitchService { get; set; } = null!;
    [Inject] private IToastService ToastService { get; set; } = null!;

    protected override void OnInitialized()
    {
        TwitchService.PropertyChanged += OnTwitchServiceOnPropertyChanged;
    }

    public void Dispose()
    {
        TwitchService.PropertyChanged -= OnTwitchServiceOnPropertyChanged;
        GC.SuppressFinalize(this);
    }

    private async void OnTwitchServiceOnPropertyChanged(object? o, PropertyChangedEventArgs propertyChangedEventArgs)
    {
        await InvokeAsync(StateHasChanged);
    }

    private async Task StartOAuthFlow() => await TwitchService.StartOAuthFlowAsync(void (error) => ToastService.ShowError(error));

    private async Task Logout() => await TwitchService.LogoutAsync();
}