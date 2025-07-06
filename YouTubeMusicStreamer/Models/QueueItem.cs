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

using System.Text.Json.Serialization;

namespace YouTubeMusicStreamer.Models;

public class QueueItem(string id, string requester, string message = "", YouTubeEmbed? embed = null)
{
    public string Id { get; init; } = id;
    public string Requester { get; init; } = requester;
    public string Message { get; init; } = message;
    public DateTime RequestedAt { get; init; } = DateTime.Now;
    public YouTubeEmbed? Embed { get; set; } = embed;
}

[method: JsonConstructor]
public class YouTubeEmbed(
    string title,
    string authorName,
    string authorUrl,
    string type,
    int height,
    int width,
    string version,
    string providerName,
    string providerUrl,
    int thumbnailHeight,
    int thumbnailWidth,
    string thumbnailUrl,
    string html)
{
    [JsonPropertyName("title")] public string Title { get; } = title;

    [JsonPropertyName("author_name")] public string AuthorName { get; } = authorName;

    [JsonPropertyName("author_url")] public string AuthorUrl { get; } = authorUrl;

    [JsonPropertyName("type")] public string Type { get; } = type;

    [JsonPropertyName("height")] public int Height { get; } = height;

    [JsonPropertyName("width")] public int Width { get; } = width;

    [JsonPropertyName("version")] public string Version { get; } = version;

    [JsonPropertyName("provider_name")] public string ProviderName { get; } = providerName;

    [JsonPropertyName("provider_url")] public string ProviderUrl { get; } = providerUrl;

    [JsonPropertyName("thumbnail_height")] public int ThumbnailHeight { get; } = thumbnailHeight;

    [JsonPropertyName("thumbnail_width")] public int ThumbnailWidth { get; } = thumbnailWidth;

    [JsonPropertyName("thumbnail_url")] public string ThumbnailUrl { get; } = thumbnailUrl;

    [JsonPropertyName("html")] public string Html { get; } = html;
}