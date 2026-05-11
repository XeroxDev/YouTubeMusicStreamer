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

namespace YouTubeMusicStreamer.Services.WebSocket;

public enum WidgetServerStatus
{
    Stopped,
    Starting,
    Running,
    Degraded,
    Error
}

public enum AudioCaptureStatus
{
    Disabled,
    Stopped,
    Starting,
    Capturing,
    Error
}

public sealed record WidgetServerConfiguration(
    int Port,
    bool AudioEnabled,
    string AudioDeviceId);

public sealed record AudioSubsystemState(
    AudioCaptureStatus Status,
    string? DeviceId,
    string? ErrorMessage);

public sealed record WidgetServerState(
    WidgetServerConfiguration Configuration,
    WidgetServerStatus Status,
    int ConnectedClients,
    object? LastTrackInfo,
    AudioSubsystemState AudioState,
    string? ErrorMessage);
