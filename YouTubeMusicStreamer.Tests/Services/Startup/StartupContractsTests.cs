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

using System.Reflection;
using System.Text.Json.Serialization;
using YouTubeMusicStreamer.Services.Startup;

namespace YouTubeMusicStreamer.Tests.Services.Startup;

public sealed class StartupContractsTests
{
    [Fact]
    public void InstanceHealthState_ExposesExpectedJsonPropertyNames_AndComputedStrings()
    {
        var state = new InstanceHealthState(
            1234,
            "launch-token",
            new DateTime(2026, 4, 6, 12, 34, 56, DateTimeKind.Utc),
            new DateTime(2026, 4, 6, 12, 35, 00, DateTimeKind.Utc),
            InstanceRuntimeState.Unhealthy,
            StartupPhase.BackgroundSubsystemsStarted,
            "1.2.3");

        Assert.Equal("Unhealthy", state.StateHumanReadable);
        Assert.Equal("BackgroundSubsystemsStarted", state.PhaseHumanReadable);

        Assert.Equal("state_h", GetJsonPropertyName(nameof(InstanceHealthState.StateHumanReadable)));
        Assert.Equal("phase_h", GetJsonPropertyName(nameof(InstanceHealthState.PhaseHumanReadable)));
    }

    [Fact]
    public void StartupIssue_AndInstanceCommandResult_PreserveTheirConstructorValues()
    {
        var issue = new StartupIssue(
            StartupIssueSeverity.Fatal,
            "Startup",
            "Failure",
            new DateTime(2026, 4, 6, 13, 0, 0, DateTimeKind.Utc),
            "detail");

        var command = new InstanceCommand(InstanceCommandType.ShowWindow);
        var result = new InstanceCommandResult(true, "Ready");

        Assert.Equal(StartupIssueSeverity.Fatal, issue.Severity);
        Assert.Equal("Startup", issue.Source);
        Assert.Equal("Failure", issue.Message);
        Assert.Equal("detail", issue.Detail);

        Assert.Equal(InstanceCommandType.ShowWindow, command.Type);
        Assert.True(result.Success);
        Assert.Equal("Ready", result.Message);
        Assert.Null(result.Health);
    }

    private static string? GetJsonPropertyName(string propertyName) =>
        typeof(InstanceHealthState)
            .GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)!
            .GetCustomAttribute<JsonPropertyNameAttribute>()?
            .Name;
}
