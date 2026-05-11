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

using System.Text.Json.Serialization;
using Microsoft.Maui.Controls;

namespace YouTubeMusicStreamer.Services.Startup;

public enum StartupPhase
{
    NotStarted,
    LoggingBootstrapReady,
    SettingsLoaded,
    CoreServicesReady,
    WindowReady,
    BackgroundSubsystemsStarted,
    Ready,
    Failed
}

public enum StartupIssueSeverity
{
    Warning,
    Degraded,
    Fatal
}

public enum InstanceRuntimeState
{
    Starting,
    Ready,
    ShuttingDown,
    Unhealthy
}

public enum InstanceCommandType
{
    Ping,
    GetHealth,
    ShowWindow,
    ShutdownIntent
}

public sealed record InstanceHealthState(
    int ProcessId,
    string LaunchToken,
    DateTime StartedAtUtc,
    DateTime LastUpdatedUtc,
    InstanceRuntimeState State,
    StartupPhase Phase,
    string AppVersion)
{
    [JsonPropertyName("state_h")]
    public string StateHumanReadable => State.ToString();

    [JsonPropertyName("phase_h")]
    public string PhaseHumanReadable => Phase.ToString();
}

public sealed record StartupIssue(
    StartupIssueSeverity Severity,
    string Source,
    string Message,
    DateTime TimestampUtc,
    string? Detail = null);

public sealed record InstanceCommand(InstanceCommandType Type);

public sealed record InstanceCommandResult(
    bool Success,
    string Message,
    InstanceHealthState? Health = null);

public interface IStartupCoordinator
{
    InstanceHealthState CurrentHealth { get; }
    IReadOnlyList<StartupIssue> CurrentIssues { get; }
    event EventHandler<InstanceHealthState> HealthChanged;
    void InitializePrimaryInstanceInfrastructure();
    void OnWindowCreated(Window? window);
    void OnWindowDestroying();
}
