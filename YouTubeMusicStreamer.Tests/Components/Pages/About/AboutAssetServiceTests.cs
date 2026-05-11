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

using System.Text;
using YouTubeMusicStreamer.Components.Pages.About;
using YouTubeMusicStreamer.Models;

namespace YouTubeMusicStreamer.Tests.Components.Pages.About;

public sealed class AboutAssetServiceTests
{
    [Fact]
    public async Task ReadPackagedTextAsync_ReturnsAssetContents()
    {
        var platform = new FakeAboutPlatformAccess();
        platform.PackageFiles["LICENSE"] = Encoding.UTF8.GetBytes("license text");
        var service = new AboutAssetService(platform);

        var content = await service.ReadPackagedTextAsync("LICENSE");

        Assert.Equal("license text", content);
    }

    [Fact]
    public async Task LoadThirdPartyLicensesAsync_ParsesCaseInsensitiveJson()
    {
        var platform = new FakeAboutPlatformAccess();
        platform.PackageFiles["Generated/third-party-licenses.json"] = Encoding.UTF8.GetBytes(
            """
            [
              {
                "packageid": "Newtonsoft.Json",
                "packageversion": "13.0.3",
                "authors": "James Newton-King",
                "licensedisplayname": "MIT",
                "licensesource": "package",
                "locallicensepath": "Generated/licenses/newtonsoft.txt",
                "onlinelicenseurl": "https://licenses.example/newtonsoft",
                "packageprojecturl": "https://www.newtonsoft.com/json",
                "copyright": "Copyright",
                "validationerrors": []
              }
            ]
            """);
        var service = new AboutAssetService(platform);

        var licenses = await service.LoadThirdPartyLicensesAsync();

        var license = Assert.Single(licenses);
        Assert.Equal("Newtonsoft.Json", license.PackageId);
        Assert.Equal("13.0.3", license.PackageVersion);
        Assert.Equal("Generated/licenses/newtonsoft.txt", license.LocalLicensePath);
    }

    [Fact]
    public async Task LoadThirdPartyLicensesAsync_ThrowsJsonException_WhenPayloadIsMalformed()
    {
        var platform = new FakeAboutPlatformAccess();
        platform.PackageFiles["Generated/third-party-licenses.json"] = Encoding.UTF8.GetBytes("{not-json");
        var service = new AboutAssetService(platform);

        await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => service.LoadThirdPartyLicensesAsync());
    }

    [Fact]
    public async Task OpenPackagedLicenseAsync_CopiesPackagedAssetToCacheAndLaunchesIt()
    {
        var platform = new FakeAboutPlatformAccess
        {
            CacheDirectory = @"C:\cache"
        };
        platform.PackageFiles["Generated/licenses/pkg-license.txt"] = Encoding.UTF8.GetBytes("license body");
        var service = new AboutAssetService(platform);
        var license = CreateLicense(
            packageId: "Pkg<Name>",
            packageVersion: "1.0/preview",
            localLicensePath: "Generated/licenses/pkg-license.txt");

        await service.OpenPackagedLicenseAsync(license);

        var output = Assert.Single(platform.CreatedFiles);
        Assert.Equal(@"C:\cache/Pkg_Name__1.0_preview.txt".Replace('/', Path.DirectorySeparatorChar), output.Key);
        Assert.Equal("license body", Encoding.UTF8.GetString(output.Value.ToArray()));
        Assert.Equal(output.Key, platform.OpenedFilePath);
    }

    [Fact]
    public async Task OpenPackagedLicenseAsync_RejectsMissingLocalLicensePath()
    {
        var service = new AboutAssetService(new FakeAboutPlatformAccess());
        var license = CreateLicense(localLicensePath: null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.OpenPackagedLicenseAsync(license));

        Assert.Equal("No packaged local license file is available for this package.", ex.Message);
    }

    [Theory]
    [InlineData("My Package", "2.0 beta", "license.txt", "My_Package__2.0_beta.txt")]
    [InlineData("A<B>C", "1.0/rc", ".md", "A_B_C__1.0_rc.md")]
    public void BuildTempLicenseFileName_SanitizesPackageIdAndVersion(
        string packageId,
        string packageVersion,
        string sourcePath,
        string expectedFileName)
    {
        var fileName = AboutAssetService.BuildTempLicenseFileName(packageId, packageVersion, sourcePath);

        Assert.Equal(expectedFileName, fileName);
    }

    private static ThirdPartyLicense CreateLicense(
        string packageId = "Package.Id",
        string packageVersion = "1.2.3",
        string? localLicensePath = "Generated/licenses/license.txt")
    {
        return new ThirdPartyLicense(
            packageId,
            packageVersion,
            "Author",
            "MIT",
            "package",
            localLicensePath,
            null,
            null,
            null,
            []);
    }

    private sealed class FakeAboutPlatformAccess : IAboutPlatformAccess
    {
        public Dictionary<string, byte[]> PackageFiles { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, MemoryStream> CreatedFiles { get; } = new(StringComparer.Ordinal);
        public string CacheDirectory { get; set; } = Path.Combine(Path.GetTempPath(), "about-tests");
        public string? OpenedFilePath { get; private set; }

        public Task<Stream> OpenAppPackageFileAsync(string assetPath)
        {
            if (!PackageFiles.TryGetValue(assetPath, out var bytes))
                throw new FileNotFoundException("Missing fake packaged asset.", assetPath);

            return Task.FromResult<Stream>(new MemoryStream(bytes, writable: false));
        }

        public Stream CreateFile(string filePath)
        {
            var stream = new MemoryStream();
            CreatedFiles[filePath] = stream;
            return stream;
        }

        public void OpenFile(string filePath)
        {
            OpenedFilePath = filePath;
        }
    }
}
