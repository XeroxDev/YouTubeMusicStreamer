using JetBrains.Annotations;
using YouTubeMusicStreamer.Attributes;
using YouTubeMusicStreamer.Services.Commands;
using YouTubeMusicStreamer.Services.Commands.Workflows;

namespace YouTubeMusicStreamer.Commands;

[Command("Starts playing a song.", false, 600, defaultResponse: "The song {title} by {author} has been started.")]
public sealed class StartCommand(IYouTubeCommandWorkflow youTubeWorkflow)
{
    [CommandExecution]
    [UsedImplicitly]
    private async Task<CommandResult<StartResult>> ExecuteCommandLogicAsync(
        CommandContext context,
        [CommandArgument(RestOfInput = true)]
        [Placeholder("The URL the user provided")] string url)
    {
        var result = await youTubeWorkflow.StartSongAsync(url);
        return result.Status switch
        {
            YouTubeWorkflowStatus.Success when result.Value is not null => CommandResult<StartResult>.Fulfilled(new StartResult
            {
                Title = result.Value.Title,
                Author = result.Value.Author,
                Channel = result.Value.Channel,
                Url = result.Value.Url
            }),
            YouTubeWorkflowStatus.InvalidInput => CommandResult<StartResult>.BadInput(CommandExecutionReason.InvalidArgument, result.Message),
            YouTubeWorkflowStatus.Blocked => CommandResult<StartResult>.Blocked(CommandExecutionReason.MissingIntegration, result.Message),
            YouTubeWorkflowStatus.Unavailable => CommandResult<StartResult>.Blocked(CommandExecutionReason.MissingIntegration, result.Message),
            _ => CommandResult<StartResult>.Blocked(CommandExecutionReason.MissingIntegration, "Unable to start the requested song.")
        };
    }

    public sealed class StartResult
    {
        [Placeholder("The title of the started song.")]
        public string Title { get; init; } = string.Empty;

        [Placeholder("The author of the started song.")]
        public string Author { get; init; } = string.Empty;

        [Placeholder("A link to the channel of the started song.")]
        public string Channel { get; init; } = string.Empty;

        [Placeholder("A link to listen to the started song.")]
        public string Url { get; init; } = string.Empty;
    }
}
