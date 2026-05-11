using JetBrains.Annotations;
using YouTubeMusicStreamer.Attributes;
using YouTubeMusicStreamer.Services.Commands;
using YouTubeMusicStreamer.Services.Commands.Workflows;

namespace YouTubeMusicStreamer.Commands;

[Command("Send information about the current song.", true, 60, "The currently playing song is {title} by {author}. It is {duration} long. You can listen to it here: {url}")]
public sealed class InfoCommand(IYouTubeCommandWorkflow youTubeWorkflow)
{
    [CommandExecution]
    [UsedImplicitly]
    private async Task<CommandResult<InfoResult>> ExecuteCommandLogicAsync(CommandContext context)
    {
        var result = await youTubeWorkflow.GetCurrentSongAsync();
        return result.Status switch
        {
            YouTubeWorkflowStatus.Success when result.Value is not null => CommandResult<InfoResult>.Fulfilled(new InfoResult
            {
                Title = result.Value.Title,
                Author = result.Value.Author,
                Duration = result.Value.Duration,
                Url = result.Value.Url
            }),
            YouTubeWorkflowStatus.InvalidInput => CommandResult<InfoResult>.BadInput(CommandExecutionReason.InvalidArgument, result.Message),
            YouTubeWorkflowStatus.Blocked => CommandResult<InfoResult>.Blocked(CommandExecutionReason.MissingIntegration, result.Message),
            YouTubeWorkflowStatus.Unavailable => CommandResult<InfoResult>.Blocked(CommandExecutionReason.MissingIntegration, result.Message),
            _ => CommandResult<InfoResult>.Blocked(CommandExecutionReason.MissingIntegration, "Current song information is unavailable.")
        };
    }

    public sealed class InfoResult
    {
        [Placeholder("The title of the currently playing song.")]
        public string Title { get; init; } = string.Empty;

        [Placeholder("The author of the currently playing song.")]
        public string Author { get; init; } = string.Empty;

        [Placeholder("The duration of the currently playing song.")]
        public string Duration { get; init; } = string.Empty;

        [Placeholder("A link to listen to the current song.")]
        public string Url { get; init; } = string.Empty;
    }
}
