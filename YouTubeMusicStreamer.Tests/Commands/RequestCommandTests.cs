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

public class RequestCommandTests
{
    [Fact]
    public async Task ExecuteCommandLogicAsync_ForwardsExecutorSourceMessageAndUrl_WhenWorkflowIsCalled()
    {
        var workflow = new FakeYouTubeCommandWorkflow
        {
            QueueSongResult = new YouTubeWorkflowResult<YouTubeSongInfo>(
                YouTubeWorkflowStatus.Success,
                new YouTubeSongInfo("Title", "Author", "Channel", "https://youtu.be/abc"))
        };
        var command = new RequestCommand(workflow);
        var context = CreateContext(
            rawInputText: "!request https://youtu.be/abc",
            sourceMessageText: "!request https://youtu.be/abc",
            executorDisplayName: "TestUser");

        var result = await CommandTestSupport.InvokeAsync(command, context, "https://youtu.be/abc");

        Assert.Equal("TestUser", workflow.LastRequestedBy);
        Assert.Equal("!request https://youtu.be/abc", workflow.LastSourceMessage);
        Assert.Equal("https://youtu.be/abc", workflow.LastUrl);
        var typed = Assert.IsType<CommandResult<RequestCommand.RequestResult>>(result);
        Assert.Equal(CommandExecutionStatus.Fulfilled, typed.Status);
        Assert.Equal("Title", typed.Value!.Title);
        Assert.Equal("Author", typed.Value.Author);
        Assert.Equal("Channel", typed.Value.Channel);
        Assert.Equal("https://youtu.be/abc", typed.Value.Url);
    }

    [Fact]
    public async Task ExecuteCommandLogicAsync_FallsBackToRawInputText_WhenSourceMessageTextIsMissing()
    {
        var workflow = new FakeYouTubeCommandWorkflow
        {
            QueueSongResult = new YouTubeWorkflowResult<YouTubeSongInfo>(
                YouTubeWorkflowStatus.Success,
                new YouTubeSongInfo("Title", "Author", "Channel", "https://youtu.be/abc"))
        };
        var command = new RequestCommand(workflow);
        var context = CreateContext(
            rawInputText: "!request https://youtu.be/abc",
            sourceMessageText: null,
            executorDisplayName: "TestUser");

        _ = await CommandTestSupport.InvokeAsync(command, context, "https://youtu.be/abc");

        Assert.Equal("!request https://youtu.be/abc", workflow.LastSourceMessage);
    }

    [Fact]
    public async Task ExecuteCommandLogicAsync_ReturnsBadInput_WhenWorkflowRejectsInput()
    {
        var workflow = new FakeYouTubeCommandWorkflow
        {
            QueueSongResult = new YouTubeWorkflowResult<YouTubeSongInfo>(
                YouTubeWorkflowStatus.InvalidInput,
                Message: "Invalid YouTube URL.")
        };
        var command = new RequestCommand(workflow);

        var result = await CommandTestSupport.InvokeAsync(command, CreateContext(), "bad-input");

        var typed = Assert.IsType<CommandResult<RequestCommand.RequestResult>>(result);
        Assert.Equal(CommandExecutionStatus.BadInput, typed.Status);
        Assert.Equal(CommandExecutionReason.InvalidArgument, typed.Reason);
        Assert.Equal("Invalid YouTube URL.", typed.Message);
        Assert.Null(typed.Value);
    }

    [Theory]
    [InlineData(YouTubeWorkflowStatus.Blocked)]
    [InlineData(YouTubeWorkflowStatus.Unavailable)]
    public async Task ExecuteCommandLogicAsync_ReturnsBlockedMissingIntegration_WhenWorkflowIsNotAvailable(YouTubeWorkflowStatus status)
    {
        var workflow = new FakeYouTubeCommandWorkflow
        {
            QueueSongResult = new YouTubeWorkflowResult<YouTubeSongInfo>(status, Message: "YTMDesktop missing.")
        };
        var command = new RequestCommand(workflow);

        var result = await CommandTestSupport.InvokeAsync(command, CreateContext(), "https://youtu.be/abc");

        var typed = Assert.IsType<CommandResult<RequestCommand.RequestResult>>(result);
        Assert.Equal(CommandExecutionStatus.Blocked, typed.Status);
        Assert.Equal(CommandExecutionReason.MissingIntegration, typed.Reason);
        Assert.Equal("YTMDesktop missing.", typed.Message);
    }

    [Fact]
    public async Task ExecuteCommandLogicAsync_ReturnsFallbackBlockedMessage_WhenSuccessHasNoSongInfo()
    {
        var workflow = new FakeYouTubeCommandWorkflow
        {
            QueueSongResult = new YouTubeWorkflowResult<YouTubeSongInfo>(YouTubeWorkflowStatus.Success, null)
        };
        var command = new RequestCommand(workflow);

        var result = await CommandTestSupport.InvokeAsync(command, CreateContext(), "https://youtu.be/abc");

        var typed = Assert.IsType<CommandResult<RequestCommand.RequestResult>>(result);
        Assert.Equal(CommandExecutionStatus.Blocked, typed.Status);
        Assert.Equal(CommandExecutionReason.MissingIntegration, typed.Reason);
        Assert.Equal("Unable to queue the requested song.", typed.Message);
    }

    private static CommandContext CreateContext(
        string rawInputText = "!request https://youtu.be/abc",
        string? sourceMessageText = "!request https://youtu.be/abc",
        string executorDisplayName = "TestUser")
        => CommandTestSupport.CreateContext("RequestCommand", "request", rawInputText, sourceMessageText, executorDisplayName);
}
