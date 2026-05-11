using JetBrains.Annotations;
using YouTubeMusicStreamer.Attributes;
using YouTubeMusicStreamer.Services.Commands;
using YouTubeMusicStreamer.Services.Commands.Workflows;

namespace YouTubeMusicStreamer.Commands;

[Command("Triggers the previous command in YTMDesktop", false, 300)]
public sealed class PrevCommand(IYouTubeCommandWorkflow youTubeWorkflow)
{
    [CommandExecution]
    [UsedImplicitly]
    private async Task<CommandResult> ExecuteCommandLogicAsync(CommandContext context)
    {
        var result = await youTubeWorkflow.PreviousAsync();
        return result.Status == YouTubeWorkflowStatus.Success
            ? CommandResult.Fulfilled()
            : CommandResult.Blocked(CommandExecutionReason.MissingIntegration, result.Message);
    }
}
