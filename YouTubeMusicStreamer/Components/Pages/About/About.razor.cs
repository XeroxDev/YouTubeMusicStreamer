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

using System.Text.Json;
using Microsoft.AspNetCore.Components;
using YouTubeMusicStreamer.Models;
using YouTubeMusicStreamer.Services.App;
using static System.Globalization.DateTimeStyles;

namespace YouTubeMusicStreamer.Components.Pages.About;

public partial class About(VersionService versionService) : ComponentBase
{
    private static JsonSerializerOptions JsonSerializerOptions => new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static string? _licenseContent;

    private static string? _additionalPermissionsContent;

    private static IReadOnlyList<ThirdPartyLicense> _thirdPartyLicenses = [];


    private static string BuildTime => DateTime.Parse(GeneratedBuildInfo.BuildTime, null, RoundtripKind).ToLocalTime().ToString("g");

    protected override async Task OnInitializedAsync()
    {
        versionService.OnChange += StateHasChanged;
        await versionService.InitializeIfNeededAsync();
        
        await OpenLicenseFileAsync();
        await OpenAdditionalPermissionsFileAsync();
        await OpenThirdPartyLicensesFileAsync();
    }

    public void Dispose()
    {
        versionService.OnChange -= StateHasChanged;
    }

    private static async Task OpenLicenseFileAsync()
    {
        if (!string.IsNullOrEmpty(_licenseContent))
            return;
        
        try
        {
            var stream = await FileSystem.OpenAppPackageFileAsync("LICENSE");
            using var reader = new StreamReader(stream);
            _licenseContent = await reader.ReadToEndAsync();
        }
        catch
        {
            _licenseContent = "Failed to load license content.";
        }
    }

    private static async Task OpenAdditionalPermissionsFileAsync()
    {
        if (!string.IsNullOrEmpty(_additionalPermissionsContent))
            return;
        
        try
        {
            var stream = await FileSystem.OpenAppPackageFileAsync("ADDITIONAL-PERMISSIONS");
            using var reader = new StreamReader(stream);
            _additionalPermissionsContent = await reader.ReadToEndAsync();
        }
        catch
        {
            _additionalPermissionsContent = "Failed to load additional permissions content.";
        }
    }

    private static async Task OpenThirdPartyLicensesFileAsync()
    {
        if (_thirdPartyLicenses.Count > 0)
            return;
        
        try
        {
            var stream = await FileSystem.OpenAppPackageFileAsync("third-party-licenses.json");
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync();
            _thirdPartyLicenses = JsonSerializer.Deserialize<IReadOnlyList<ThirdPartyLicense>>(json, JsonSerializerOptions) ?? [];
        }
        catch
        {
            _thirdPartyLicenses = [];
        }
    }
}