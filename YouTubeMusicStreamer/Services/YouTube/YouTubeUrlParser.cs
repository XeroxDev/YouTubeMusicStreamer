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

using System.Web;

namespace YouTubeMusicStreamer.Services.YouTube;

public static class YouTubeUrlParser
{
    private static readonly string[] YouTubeHosts =
    [
        "youtube.com",
        "www.youtube.com",
        "m.youtube.com",
        "music.youtube.com"
    ];

    public static string? GetVideoId(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return null;

        if (IsShortHost(uri.Host))
            return GetPathSegmentVideoId(uri);

        if (!IsStandardYouTubeHost(uri.Host))
            return null;

        var query = HttpUtility.ParseQueryString(uri.Query);
        var queryVideoId = query["v"];
        if (!string.IsNullOrWhiteSpace(queryVideoId))
            return queryVideoId;

        return GetPathBasedVideoId(uri.AbsolutePath);
    }

    private static bool IsShortHost(string host) =>
        string.Equals(host, "youtu.be", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(host, "www.youtu.be", StringComparison.OrdinalIgnoreCase);

    private static bool IsStandardYouTubeHost(string host) =>
        YouTubeHosts.Any(allowedHost => string.Equals(host, allowedHost, StringComparison.OrdinalIgnoreCase));

    private static string? GetPathSegmentVideoId(Uri uri)
    {
        var path = uri.AbsolutePath.Trim('/');
        if (string.IsNullOrWhiteSpace(path))
            return null;

        return path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
    }

    private static string? GetPathBasedVideoId(string absolutePath)
    {
        var segments = absolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments.Length < 2)
            return null;

        return segments[0].ToLowerInvariant() switch
        {
            "shorts" => segments[1],
            "live" => segments[1],
            "embed" => segments[1],
            "v" => segments[1],
            _ => null
        };
    }
}
