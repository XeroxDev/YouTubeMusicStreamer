// This file is part of YouTubeMusicStreamer.
// Copyright (C) 2026 Dominic Ris
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

using System.Diagnostics;
using System.Text.Json;
using YouTubeMusicStreamer.Models;

namespace YouTubeMusicStreamer.Components.Pages.About;

public interface IAboutAssetService
{
    Task<string> ReadPackagedTextAsync(string assetPath);
    Task<IReadOnlyList<ThirdPartyLicense>> LoadThirdPartyLicensesAsync();
    Task OpenPackagedLicenseAsync(ThirdPartyLicense license);
}

internal interface IAboutPlatformAccess
{
    Task<Stream> OpenAppPackageFileAsync(string assetPath);
    string CacheDirectory { get; }
    Stream CreateFile(string filePath);
    void OpenFile(string filePath);
}

internal sealed class MauiAboutPlatformAccess : IAboutPlatformAccess
{
    public Task<Stream> OpenAppPackageFileAsync(string assetPath) => FileSystem.OpenAppPackageFileAsync(assetPath);

    public string CacheDirectory => FileSystem.Current.CacheDirectory;

    public Stream CreateFile(string filePath) => File.Create(filePath);

    public void OpenFile(string filePath)
    {
        Process.Start(new ProcessStartInfo(filePath)
        {
            UseShellExecute = true
        });
    }
}

internal sealed class AboutAssetService(IAboutPlatformAccess platformAccess) : IAboutAssetService
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private const string ThirdPartyLicensesAssetPath = "Generated/third-party-licenses.json";

    public async Task<string> ReadPackagedTextAsync(string assetPath)
    {
        await using var stream = await platformAccess.OpenAppPackageFileAsync(assetPath);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    public async Task<IReadOnlyList<ThirdPartyLicense>> LoadThirdPartyLicensesAsync()
    {
        var json = await ReadPackagedTextAsync(ThirdPartyLicensesAssetPath);
        return JsonSerializer.Deserialize<IReadOnlyList<ThirdPartyLicense>>(json, JsonSerializerOptions) ?? [];
    }

    public async Task OpenPackagedLicenseAsync(ThirdPartyLicense license)
    {
        if (string.IsNullOrWhiteSpace(license.LocalLicensePath))
            throw new InvalidOperationException("No packaged local license file is available for this package.");

        await using var packagedStream = await platformAccess.OpenAppPackageFileAsync(license.LocalLicensePath);
        var tempFilePath = Path.Combine(
            platformAccess.CacheDirectory,
            BuildTempLicenseFileName(license.PackageId, license.PackageVersion, license.LocalLicensePath));

        await using (var fileStream = platformAccess.CreateFile(tempFilePath))
        {
            await packagedStream.CopyToAsync(fileStream);
        }

        platformAccess.OpenFile(tempFilePath);
    }

    internal static string BuildTempLicenseFileName(string packageId, string packageVersion, string sourcePath)
    {
        var extension = Path.GetExtension(sourcePath);
        return $"{SanitizeFileNamePart(packageId)}__{SanitizeFileNamePart(packageVersion)}{extension}";
    }

    internal static string SanitizeFileNamePart(string value) =>
        string.Join("_", value.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Replace(' ', '_');
}
