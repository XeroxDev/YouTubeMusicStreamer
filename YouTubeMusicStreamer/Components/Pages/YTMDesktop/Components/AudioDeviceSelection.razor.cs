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

using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using YouTubeMusicStreamer.Services;
using YouTubeMusicStreamer.Services.App;
using AudioService = YouTubeMusicStreamer.Services.App.AudioService;

namespace YouTubeMusicStreamer.Components.Pages.YTMDesktop.Components;

public partial class AudioDeviceSelection
{
    private List<AudioDeviceInfo> _audioDevices = AudioService.GetDevices();
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

    private async Task ReloadAudioDevices()
    {
        _refreshing = true;
        await Task.Run(() => _audioDevices = AudioService.GetDevices(true));
        await Task.Delay(100);
        _refreshing = false;
    }
}