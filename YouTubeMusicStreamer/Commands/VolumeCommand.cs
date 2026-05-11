using JetBrains.Annotations;
using YouTubeMusicStreamer.Attributes;
using YouTubeMusicStreamer.Services.Commands;
using YouTubeMusicStreamer.Services.Commands.Workflows;

namespace YouTubeMusicStreamer.Commands;

[Command("Changes the volume", false, 300)]
public sealed class VolumeCommand(IYouTubeCommandWorkflow youTubeWorkflow)
{
    [CommandExecution]
    [UsedImplicitly]
    private async Task<CommandResult<VolumeResult>> ExecuteCommandLogicAsync(
        CommandContext context,
        [Placeholder("The volume the user provided")] int volume)
    {
        var result = await youTubeWorkflow.SetVolumeAsync(volume);
        return result.Status == YouTubeWorkflowStatus.Success
            ? CommandResult<VolumeResult>.Fulfilled(new VolumeResult { Volume = result.Value })
            : CommandResult<VolumeResult>.Blocked(CommandExecutionReason.MissingIntegration, result.Message);
    }

    public sealed class VolumeResult
    {
        [Placeholder("The volume that was set.")]
        public int Volume { get; init; }
    }
}
