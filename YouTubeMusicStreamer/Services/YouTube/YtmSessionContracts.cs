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

using XeroxDev.YTMDesktop.Companion.Enums;
using XeroxDev.YTMDesktop.Companion.Models.Output;

namespace YouTubeMusicStreamer.Services.YouTube;

public enum YtmEndpointStatus
{
    Unknown,
    NotConfigured,
    ValidCompanion,
    Unreachable,
    IncompatibleEndpoint
}

public enum YtmAuthorizationStatus
{
    NotConfigured,
    AuthRequired,
    Authorizing,
    Authorized,
    AuthFailed
}

public enum YtmConnectionStatus
{
    Disconnected,
    Connecting,
    Connected,
    Retrying,
    Degraded,
    Error
}

public sealed record YtmEndpointConfiguration(
    string? Host,
    int? Port);

public sealed record YtmPlaybackSnapshot(
    StateOutput State,
    DateTimeOffset ReceivedAtUtc);

public sealed record YtmCompanionSessionState(
    YtmEndpointConfiguration Endpoint,
    YtmEndpointStatus EndpointStatus,
    YtmAuthorizationStatus AuthorizationStatus,
    YtmConnectionStatus ConnectionStatus,
    ESocketState SocketState,
    string? ErrorMessage,
    string? AuthorizationCode,
    bool HasStoredToken,
    YtmPlaybackSnapshot? PlaybackSnapshot);
