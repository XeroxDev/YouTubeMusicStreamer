using JetBrains.Annotations;
using YouTubeMusicStreamer.Attributes;
using YouTubeMusicStreamer.Services.Commands;
using YouTubeMusicStreamer.Services.Commands.Workflows;

namespace YouTubeMusicStreamer.Commands;

[Command("Changes the volume to a random value", false, 300)]
public sealed class RandomVolumeCommand(IYouTubeCommandWorkflow youTubeWorkflow)
{
    [CommandExecution]
    [UsedImplicitly]
    private async Task<CommandResult<RandomVolumeResult>> ExecuteCommandLogicAsync(CommandContext context)
    {
        var result = await youTubeWorkflow.SetRandomVolumeAsync();
        return result.Status == YouTubeWorkflowStatus.Success
            ? CommandResult<RandomVolumeResult>.Fulfilled(new RandomVolumeResult { Volume = result.Value })
            : CommandResult<RandomVolumeResult>.Blocked(CommandExecutionReason.MissingIntegration, result.Message);
    }

    public sealed class RandomVolumeResult
    {
        [Placeholder("The random volume that was set.")]
        public int Volume { get; init; }
    }
}
