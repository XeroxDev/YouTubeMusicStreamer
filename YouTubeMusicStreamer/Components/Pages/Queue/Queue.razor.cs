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

using Blazored.Toast.Services;
using Microsoft.AspNetCore.Components;
using YouTubeMusicStreamer.Models;
using YouTubeMusicStreamer.Services.App;

namespace YouTubeMusicStreamer.Components.Pages.Queue;

public partial class Queue(SettingsService settingsService, IToastService toastService) : ComponentBase, IDisposable
{
    private List<QueueItem> _queueItems = [];
    private bool _queueActive;
    private bool _queueItemAsIFrame;

    private async Task RemoveItem(QueueItem item)
    {
        await settingsService.SaveAppSettingAsync(s => s.Queue.Remove(item));
    }

    private async Task ClearQueue()
    {
        await settingsService.SaveAppSettingAsync(s => s.Queue.Clear());
    }

    private async Task MoveItemUp(QueueItem item)
    {
        var index = _queueItems.IndexOf(item);
        if (index == 0) return;

        await settingsService.SaveAppSettingAsync(s =>
        {
            s.Queue.Remove(item);
            s.Queue.Insert(index - 1, item);
        });
    }

    private async Task MoveItemDown(QueueItem item)
    {
        var index = _queueItems.IndexOf(item);
        if (index == _queueItems.Count - 1) return;

        await settingsService.SaveAppSettingAsync(s =>
        {
            s.Queue.Remove(item);
            s.Queue.Insert(index + 1, item);
        });
    }

    private async Task MoveItemToTop(QueueItem item)
    {
        await settingsService.SaveAppSettingAsync(s =>
        {
            s.Queue.Remove(item);
            s.Queue.Insert(0, item);
        });
    }

    private async Task MoveItemToBottom(QueueItem item)
    {
        await settingsService.SaveAppSettingAsync(s =>
        {
            s.Queue.Remove(item);
            s.Queue.Add(item);
        });
    }
    

    protected override void OnInitialized()
    {
        settingsService.AppSettingsChanged += OnAppSettingsChanged;
        _queueItems = settingsService.GetAppSettings().Queue.ToList();
        ResetGlobalSettings(false);
    }

    private void OnAppSettingsChanged(AppSettings obj)
    {
        _queueItems = obj.Queue;
        Task.Delay(1).ContinueWith(_ => InvokeAsync(StateHasChanged));
    }

    public void Dispose()
    {
        settingsService.AppSettingsChanged -= OnAppSettingsChanged;
        GC.SuppressFinalize(this);
    }

    private async Task SaveGlobalSettings()
    {
        await settingsService.SaveAppSettingAsync(s =>
        {
            s.QueueActive = _queueActive;
            s.QueueItemAsIFrame = _queueItemAsIFrame;
        });

        toastService.ShowSuccess("Settings saved successfully.");
    }

    private void ResetGlobalSettings(bool notify = true)
    {
        _queueActive = settingsService.GetAppSettings().QueueActive;
        _queueItemAsIFrame = settingsService.GetAppSettings().QueueItemAsIFrame;

        if (notify)
        {
            toastService.ShowSuccess("Settings reset to previous saved state.");
        }
    }
}