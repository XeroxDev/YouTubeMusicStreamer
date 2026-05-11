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

namespace YouTubeMusicStreamer.Services.Commands.Workflows;

public enum YouTubeWorkflowStatus
{
    Success,
    InvalidInput,
    Blocked,
    Unavailable
}

public sealed record YouTubeWorkflowResult<T>(
    YouTubeWorkflowStatus Status,
    T? Value = default,
    string? Message = null);

public sealed record YouTubeSongInfo(
    string Title,
    string Author,
    string Channel,
    string Url,
    string Duration = "");

public interface IYouTubeCommandWorkflow
{
    Task<YouTubeWorkflowResult<YouTubeSongInfo>> GetCurrentSongAsync();
    Task<YouTubeWorkflowResult<YouTubeSongInfo>> QueueSongAsync(string requestedBy, string sourceMessage, string url);
    Task<YouTubeWorkflowResult<YouTubeSongInfo>> StartSongAsync(string url);
    Task<YouTubeWorkflowResult<bool>> NextAsync();
    Task<YouTubeWorkflowResult<bool>> PreviousAsync();
    Task<YouTubeWorkflowResult<int>> SetRandomVolumeAsync();
    Task<YouTubeWorkflowResult<int>> SetVolumeAsync(int volume);
}
