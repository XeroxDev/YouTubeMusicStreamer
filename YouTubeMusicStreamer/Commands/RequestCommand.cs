using JetBrains.Annotations;
using YouTubeMusicStreamer.Attributes;
using YouTubeMusicStreamer.Services.Commands;
using YouTubeMusicStreamer.Services.Commands.Workflows;

namespace YouTubeMusicStreamer.Commands;

[Command("Adds a song to the queue.", defaultResponse: "The song {title} by {author} has been added to the queue.")]
public sealed class RequestCommand(IYouTubeCommandWorkflow youTubeWorkflow)
{
    [CommandExecution]
    [UsedImplicitly]
    private async Task<CommandResult<RequestResult>> ExecuteCommandLogicAsync(
        CommandContext context,
        [CommandArgument(RestOfInput = true)]
        [Placeholder("The URL the user provided")] string url)
    {
        var sourceMessage = context.SourceMessage.Message?.Text ?? context.RawInputText;
        var result = await youTubeWorkflow.QueueSongAsync(context.ExecutorDisplayName, sourceMessage, url);
        return result.Status switch
        {
            YouTubeWorkflowStatus.Success when result.Value is not null => CommandResult<RequestResult>.Fulfilled(new RequestResult
            {
                Title = result.Value.Title,
                Author = result.Value.Author,
                Channel = result.Value.Channel,
                Url = result.Value.Url
            }),
            YouTubeWorkflowStatus.InvalidInput => CommandResult<RequestResult>.BadInput(CommandExecutionReason.InvalidArgument, result.Message),
            YouTubeWorkflowStatus.Blocked => CommandResult<RequestResult>.Blocked(CommandExecutionReason.MissingIntegration, result.Message),
            YouTubeWorkflowStatus.Unavailable => CommandResult<RequestResult>.Blocked(CommandExecutionReason.MissingIntegration, result.Message),
            _ => CommandResult<RequestResult>.Blocked(CommandExecutionReason.MissingIntegration, "Unable to queue the requested song.")
        };
    }

    public sealed class RequestResult
    {
        [Placeholder("The title of the requested song.")]
        public string Title { get; init; } = string.Empty;

        [Placeholder("The author of the requested song.")]
        public string Author { get; init; } = string.Empty;

        [Placeholder("The channel of the requested song.")]
        public string Channel { get; init; } = string.Empty;

        [Placeholder("A link to the requested song.")]
        public string Url { get; init; } = string.Empty;
    }
}
