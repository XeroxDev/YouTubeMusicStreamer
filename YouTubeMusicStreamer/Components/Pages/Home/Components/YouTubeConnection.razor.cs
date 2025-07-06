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
using YouTubeService = YouTubeMusicStreamer.Services.YouTube.YouTubeService;

namespace YouTubeMusicStreamer.Components.Pages.Home.Components;

public partial class YouTubeConnection : ComponentBase, IDisposable
{
    [Inject] private YouTubeService YouTubeService { get; set; } = null!;

    private string _host = string.Empty;
    private int _port;
    private bool _disposed;

    protected override void OnInitialized()
    {
        _host = YouTubeService.GetValidatedHost();
        _port = YouTubeService.GetValidatedPort();
        YouTubeService.PropertyChanged += OnYouTubeServiceOnPropertyChanged;
    }

    public void Dispose()
    {
        _disposed = true;
        YouTubeService.PropertyChanged -= OnYouTubeServiceOnPropertyChanged;
        GC.SuppressFinalize(this);
    }

    private async void OnYouTubeServiceOnPropertyChanged(object? o, PropertyChangedEventArgs propertyChangedEventArgs)
    {
        try
        {
            if (_disposed) return;
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception)
        {
            // ignore
        }
    }

    private async Task ConnectAsync() => await YouTubeService.SetAddressAsync(_host, _port);
}