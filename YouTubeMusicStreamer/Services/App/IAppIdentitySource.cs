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

using System.Reflection;

namespace YouTubeMusicStreamer.Services.App;

public interface IAppIdentitySource
{
    string AppName { get; }
    string AppId { get; }
}

public sealed class AssemblyAppIdentitySource : IAppIdentitySource
{
    public static string DefaultAppName => Assembly.GetExecutingAssembly().GetName().Name ?? "YouTubeMusicStreamer";

    public string AppName => DefaultAppName;

    public string AppId => DefaultAppName.ToLowerInvariant().Replace(" ", "-");
}
