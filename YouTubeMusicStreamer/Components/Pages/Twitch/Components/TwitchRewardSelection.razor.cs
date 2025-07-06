using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using TwitchService = YouTubeMusicStreamer.Services.Twitch.TwitchService;

namespace YouTubeMusicStreamer.Components.Pages.Twitch.Components;

public partial class TwitchRewardSelection(TwitchService twitchService) : ComponentBase
{
    private bool _refreshing;

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

    protected override Task OnInitializedAsync()
    {
        twitchService.PropertyChanged += async (_, args) =>
        {
            if (args.PropertyName != nameof(twitchService.Rewards)) return;
            await Task.Delay(100);
            _refreshing = false;
            StateHasChanged();
        };
        return Task.CompletedTask;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            if (twitchService.Rewards.Count == 0)
            {
                await ReloadRewards();
            }
            else
            {
                _refreshing = false;
            }
        }

        await base.OnAfterRenderAsync(firstRender);
    }

    private async Task ReloadRewards()
    {
        _refreshing = true;
        await twitchService.RefreshRewardsAsync();
    }
}