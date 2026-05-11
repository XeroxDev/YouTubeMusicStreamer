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

using YouTubeMusicStreamer.Commands;
using YouTubeMusicStreamer.Services.Commands;
using YouTubeMusicStreamer.Services.Commands.Workflows;
using YouTubeMusicStreamer.Tests.TestSupport;

namespace YouTubeMusicStreamer.Tests.Commands;

public class VolumeCommandTests
{
    [Fact]
    public async Task ExecuteCommandLogicAsync_ForwardsVolumeAndReturnsValue_WhenWorkflowSucceeds()
    {
        var workflow = new FakeYouTubeCommandWorkflow
        {
            VolumeResult = new YouTubeWorkflowResult<int>(YouTubeWorkflowStatus.Success, 75)
        };
        var command = new VolumeCommand(workflow);

        var result = await CommandTestSupport.InvokeAsync(
            command,
            CommandTestSupport.CreateContext("VolumeCommand", "volume", "!volume 75"),
            75);

        Assert.Equal(75, workflow.LastVolume);
        var typed = Assert.IsType<CommandResult<VolumeCommand.VolumeResult>>(result);
        Assert.Equal(CommandExecutionStatus.Fulfilled, typed.Status);
        Assert.Equal(75, typed.Value!.Volume);
    }

    [Fact]
    public async Task ExecuteCommandLogicAsync_ReturnsBlockedMissingIntegration_WhenWorkflowFails()
    {
        var workflow = new FakeYouTubeCommandWorkflow
        {
            VolumeResult = new YouTubeWorkflowResult<int>(YouTubeWorkflowStatus.Unavailable, Message: "YTMDesktop missing.")
        };
        var command = new VolumeCommand(workflow);

        var result = await CommandTestSupport.InvokeAsync(
            command,
            CommandTestSupport.CreateContext("VolumeCommand", "volume", "!volume 75"),
            75);

        var typed = Assert.IsType<CommandResult<VolumeCommand.VolumeResult>>(result);
        Assert.Equal(CommandExecutionStatus.Blocked, typed.Status);
        Assert.Equal(CommandExecutionReason.MissingIntegration, typed.Reason);
        Assert.Equal("YTMDesktop missing.", typed.Message);
    }
}
