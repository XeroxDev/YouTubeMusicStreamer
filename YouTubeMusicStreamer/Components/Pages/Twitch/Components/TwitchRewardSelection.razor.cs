// This file is part of YouTubeMusicStreamer.
// Copyright (C) 2025 Dominic Ris
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
using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using YouTubeMusicStreamer.Services.Twitch;

namespace YouTubeMusicStreamer.Components.Pages.Twitch.Components;

public partial class TwitchRewardSelection(TwitchRewardsFacade twitchRewardsFacade) : ComponentBase, IDisposable
{
    private string _value = string.Empty;

    [Parameter]
#pragma warning disable BL0007
    public string Value
#pragma warning restore BL0007
    {
        get => _value;
        set
        {
            if (_value == value) return;
            _value = value;
            ValueChanged.InvokeAsync(value);
        }
    }

    [Parameter] public EventCallback<string> ValueChanged { get; set; }
    [Parameter] public Expression<Func<string>>? ValueExpression { get; set; }
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public bool IncludeDisabledOption { get; set; } = true;

    protected override Task OnInitializedAsync()
    {
        twitchRewardsFacade.Initialize();
        twitchRewardsFacade.StateChanged += OnRewardsFacadeStateChanged;
        return Task.CompletedTask;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            if (twitchRewardsFacade.Rewards.Count == 0)
            {
                await ReloadRewardsAsync();
            }
        }

        await base.OnAfterRenderAsync(firstRender);
    }

    private void OnRewardsFacadeStateChanged(object? sender, EventArgs args)
    {
        _ = InvokeAsync(StateHasChanged);
    }

    private async Task ReloadRewardsAsync() => await twitchRewardsFacade.RefreshAsync();

    public void Dispose()
    {
        twitchRewardsFacade.StateChanged -= OnRewardsFacadeStateChanged;
        twitchRewardsFacade.Dispose();
        GC.SuppressFinalize(this);
    }
}
