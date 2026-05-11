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
using static System.Globalization.DateTimeStyles;

namespace YouTubeMusicStreamer.Components.Pages.About;

public partial class About(VersionService versionService, IAboutAssetService aboutAssetService) : ComponentBase, IDisposable
{
    private readonly IAboutAssetService _aboutAssetService = aboutAssetService;

    private static string? _licenseContent;

    private static string? _additionalPermissionsContent;

    private static IReadOnlyList<ThirdPartyLicense> _thirdPartyLicenses = [];
    private static bool _thirdPartyLicensesLoaded;
    private static string? _thirdPartyLicensesError;
    private static string? _thirdPartyLicenseActionError;


    private static string BuildTime => DateTime.Parse(GeneratedBuildInfo.BuildTime, null, RoundtripKind).ToLocalTime().ToString("g");

    protected override async Task OnInitializedAsync()
    {
        versionService.OnChange += OnVersionServiceChanged;
        await versionService.InitializeIfNeededAsync();
        
        await OpenLicenseFileAsync();
        await OpenAdditionalPermissionsFileAsync();
        await OpenThirdPartyLicensesFileAsync();
    }

    public void Dispose()
    {
        versionService.OnChange -= OnVersionServiceChanged;
        GC.SuppressFinalize(this);
    }

    private void OnVersionServiceChanged() => _ = InvokeAsync(StateHasChanged);

    private async Task OpenLicenseFileAsync()
    {
        if (!string.IsNullOrEmpty(_licenseContent))
            return;
        
        try
        {
            _licenseContent = await _aboutAssetService.ReadPackagedTextAsync("LICENSE");
        }
        catch
        {
            _licenseContent = "Failed to load license content.";
        }
    }

    private async Task OpenAdditionalPermissionsFileAsync()
    {
        if (!string.IsNullOrEmpty(_additionalPermissionsContent))
            return;
        
        try
        {
            _additionalPermissionsContent = await _aboutAssetService.ReadPackagedTextAsync("ADDITIONAL-PERMISSIONS");
        }
        catch
        {
            _additionalPermissionsContent = "Failed to load additional permissions content.";
        }
    }

    private async Task OpenThirdPartyLicensesFileAsync()
    {
        if (_thirdPartyLicensesLoaded)
            return;
        
        try
        {
            _thirdPartyLicenses = await _aboutAssetService.LoadThirdPartyLicensesAsync();
            _thirdPartyLicensesError = null;
            _thirdPartyLicenseActionError = null;
        }
        catch (Exception ex)
        {
            _thirdPartyLicenses = [];
            _thirdPartyLicensesError = $"Failed to load packaged third-party licenses: {ex.Message}";
        }
        finally
        {
            _thirdPartyLicensesLoaded = true;
        }
    }

    private async Task OpenThirdPartyLicenseAsync(ThirdPartyLicense license)
    {
        if (string.IsNullOrWhiteSpace(license.LocalLicensePath))
        {
            _thirdPartyLicenseActionError = "No packaged local license file is available for this package.";
            await InvokeAsync(StateHasChanged);
            return;
        }

        try
        {
            await _aboutAssetService.OpenPackagedLicenseAsync(license);
            _thirdPartyLicenseActionError = null;
        }
        catch (Exception ex)
        {
            _thirdPartyLicenseActionError = $"Failed to open packaged license file: {ex.Message}";
        }

        await InvokeAsync(StateHasChanged);
    }
}
