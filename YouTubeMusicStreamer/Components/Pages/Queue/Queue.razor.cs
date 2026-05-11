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

using Microsoft.AspNetCore.Components;
using YouTubeMusicStreamer.Models;
using YouTubeMusicStreamer.Services.App;
using YouTubeMusicStreamer.Services.App.Persistence;
using YouTubeMusicStreamer.Services.YouTube;

namespace YouTubeMusicStreamer.Components.Pages.Queue;

public partial class Queue(QueuePageFacade queueFacade, IAppToastService toastService) : ComponentBase, IDisposable
{
    private List<QueueItem> _queueItems = [];
    private bool _queueActive;

    private async Task RemoveItem(QueueItem item)
    {
        await queueFacade.RemoveItemAsync(item);
    }

    private async Task ClearQueue()
    {
        await queueFacade.ClearQueueAsync();
    }

    private async Task MoveItemUp(QueueItem item)
    {
        await queueFacade.MoveItemUpAsync(item);
    }

    private async Task MoveItemDown(QueueItem item)
    {
        await queueFacade.MoveItemDownAsync(item);
    }

    private async Task MoveItemToTop(QueueItem item)
    {
        await queueFacade.MoveItemToTopAsync(item);
    }

    private async Task MoveItemToBottom(QueueItem item)
    {
        await queueFacade.MoveItemToBottomAsync(item);
    }
    

    protected override void OnInitialized()
    {
        queueFacade.Initialize();
        queueFacade.StateChanged += OnFacadeStateChanged;
        RefreshState(resetDraftSettings: true);
    }

    private void OnFacadeStateChanged(object? sender, EventArgs e)
    {
        RefreshState();
        _ = InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        queueFacade.StateChanged -= OnFacadeStateChanged;
        queueFacade.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task SaveGlobalSettings()
    {
        await queueFacade.SaveSettingsAsync(new QueueSettingsSnapshot(_queueActive));

        toastService.ShowSuccess("Settings saved successfully.");
    }

    private void ResetGlobalSettings(bool notify = true)
    {
        _queueActive = queueFacade.Settings.QueueActive;

        if (notify)
        {
            toastService.ShowSuccess("Settings reset to previous saved state.");
        }
    }

    private void RefreshState(bool resetDraftSettings = false)
    {
        _queueItems = queueFacade.QueueItems.ToList();

        if (resetDraftSettings)
        {
            ResetGlobalSettings(false);
        }
    }
}
