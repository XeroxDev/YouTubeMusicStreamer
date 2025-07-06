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

namespace YouTubeMusicStreamer.Components.Shared;

public partial class Toggle
{
    private bool _value;

    [Parameter]
#pragma warning disable BL0007
    public bool Value
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

    [Parameter] public EventCallback<bool> ValueChanged { get; set; }
    [Parameter] public Expression<Func<bool>>? ValueExpression { get; set; }
    [Parameter] public string OnLabel { get; set; } = "On";
    [Parameter] public string OffLabel { get; set; } = "Off";
    [Parameter] public string MainCssClass { get; set; } = string.Empty;
    [Parameter] public string DivCssClass { get; set; } = "field";
    [Parameter] public string LabelCssClass { get; set; } = string.Empty;
    [Parameter] public string Id { get; set; } = Guid.NewGuid().ToString("N");
}