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

using System.Net;

namespace YouTubeMusicStreamer.Services.Twitch.Interfaces;

public enum TwitchAuthStatus
{
    Success,
    Cancelled,
    Failed
}

public sealed record TwitchAuthResult(
    TwitchAuthStatus Status,
    string? AccessToken,
    string? ErrorMessage);

public interface ITwitchAuthService
{
    Task<TwitchAuthResult> StartAsync(TwitchAccountRole role, CancellationToken cancellationToken = default);
}

public interface ITwitchAuthBrowserLauncher
{
    Task OpenAsync(Uri uri);
}

public interface ITwitchAuthCallbackListenerFactory
{
    ITwitchAuthCallbackListener Create(int port);
}

public interface ITwitchAuthCallbackListener : IDisposable
{
    void Start();
    void Stop();
    Task<ITwitchAuthRequestContext> GetContextAsync(CancellationToken cancellationToken);
}

public interface ITwitchAuthRequestContext
{
    string HttpMethod { get; }
    string? Path { get; }
    Task<string> ReadBodyAsync();
    Task WriteHtmlAsync(string html);
    Task WritePlainTextAsync(HttpStatusCode statusCode, string message);
}
