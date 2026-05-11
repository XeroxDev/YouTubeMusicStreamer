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

using System.Text.Json;
using YouTubeMusicStreamer.Services.App;

namespace YouTubeMusicStreamer.Services.Startup;

public sealed class FileStartupStateStore : IStartupStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly IAppPathProvider _appPathProvider;

    public FileStartupStateStore(IAppPathProvider appPathProvider)
    {
        _appPathProvider = appPathProvider;
    }

    public void Persist(InstanceHealthState health)
    {
        File.WriteAllText(_appPathProvider.InstanceStateFilePath, JsonSerializer.Serialize(health, JsonOptions));
    }
}
