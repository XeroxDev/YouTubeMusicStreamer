// This file is part of YouTubeMusicStreamer.
// Copyright (C) 2026 Dominic Ris
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

using NuGet.Versioning;

namespace YouTubeMusicStreamer.Services.App;

public interface IAppVersionSource
{
    SemanticVersion GetInternalAppVersion();
}

public sealed class MauiAppVersionSource : IAppVersionSource
{
    public SemanticVersion GetInternalAppVersion()
    {
        var versionString = AppInfo.Current.VersionString;
        if (string.IsNullOrWhiteSpace(versionString))
            return new SemanticVersion(0, 0, 0);

        var parts = versionString.Split('.');
        int major = 0, minor = 0, patch = 0;
        if (parts.Length > 0 && int.TryParse(parts[0], out var m)) major = m;
        if (parts.Length > 1 && int.TryParse(parts[1], out var n)) minor = n;
        if (parts.Length > 2 && int.TryParse(parts[2], out var p)) patch = p;
        return new SemanticVersion(major, minor, patch);
    }
}
