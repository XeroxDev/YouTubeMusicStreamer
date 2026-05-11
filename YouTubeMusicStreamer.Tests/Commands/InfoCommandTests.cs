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

public class InfoCommandTests
{
    [Fact]
    public async Task ExecuteCommandLogicAsync_ReturnsCurrentSongInfo_WhenWorkflowSucceeds()
    {
        var workflow = new FakeYouTubeCommandWorkflow
        {
            CurrentSongResult = new YouTubeWorkflowResult<YouTubeSongInfo>(
                YouTubeWorkflowStatus.Success,
                new YouTubeSongInfo("Title", "Author", "Channel", "https://youtu.be/abc", "03:32"))
        };
        var command = new InfoCommand(workflow);

        var result = await CommandTestSupport.InvokeAsync(
            command,
            CommandTestSupport.CreateContext("InfoCommand", "info", "!info"));

        Assert.Equal(1, workflow.GetCurrentSongCallCount);
        var typed = Assert.IsType<CommandResult<InfoCommand.InfoResult>>(result);
        Assert.Equal(CommandExecutionStatus.Fulfilled, typed.Status);
        Assert.Equal("Title", typed.Value!.Title);
        Assert.Equal("Author", typed.Value.Author);
        Assert.Equal("03:32", typed.Value.Duration);
        Assert.Equal("https://youtu.be/abc", typed.Value.Url);
    }

    [Theory]
    [InlineData(YouTubeWorkflowStatus.InvalidInput, CommandExecutionStatus.BadInput, CommandExecutionReason.InvalidArgument)]
    [InlineData(YouTubeWorkflowStatus.Blocked, CommandExecutionStatus.Blocked, CommandExecutionReason.MissingIntegration)]
    [InlineData(YouTubeWorkflowStatus.Unavailable, CommandExecutionStatus.Blocked, CommandExecutionReason.MissingIntegration)]
    public async Task ExecuteCommandLogicAsync_MapsWorkflowFailures_WhenWorkflowDoesNotReturnSongInfo(
        YouTubeWorkflowStatus status,
        CommandExecutionStatus expectedStatus,
        CommandExecutionReason expectedReason)
    {
        var workflow = new FakeYouTubeCommandWorkflow
        {
            CurrentSongResult = new YouTubeWorkflowResult<YouTubeSongInfo>(status, Message: "No song.")
        };
        var command = new InfoCommand(workflow);

        var result = await CommandTestSupport.InvokeAsync(
            command,
            CommandTestSupport.CreateContext("InfoCommand", "info", "!info"));

        var typed = Assert.IsType<CommandResult<InfoCommand.InfoResult>>(result);
        Assert.Equal(expectedStatus, typed.Status);
        Assert.Equal(expectedReason, typed.Reason);
        Assert.Equal("No song.", typed.Message);
    }

    [Fact]
    public async Task ExecuteCommandLogicAsync_ReturnsFallbackBlockedMessage_WhenWorkflowSuccessHasNoSongInfo()
    {
        var workflow = new FakeYouTubeCommandWorkflow
        {
            CurrentSongResult = new YouTubeWorkflowResult<YouTubeSongInfo>(YouTubeWorkflowStatus.Success, null)
        };
        var command = new InfoCommand(workflow);

        var result = await CommandTestSupport.InvokeAsync(
            command,
            CommandTestSupport.CreateContext("InfoCommand", "info", "!info"));

        var typed = Assert.IsType<CommandResult<InfoCommand.InfoResult>>(result);
        Assert.Equal(CommandExecutionStatus.Blocked, typed.Status);
        Assert.Equal("Current song information is unavailable.", typed.Message);
    }
}
