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

using TwitchLib.EventSub.Core.Models.Chat;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;
using YouTubeMusicStreamer.Services.App.Persistence;
using YouTubeMusicStreamer.Services.Commands;
using YouTubeMusicStreamer.Services.Commands.Binding;
using YouTubeMusicStreamer.Services.Commands.Workflows;
using YouTubeMusicStreamer.Services.Twitch.Interfaces;

namespace YouTubeMusicStreamer.Tests.TestSupport;

internal static class CommandTestSupport
{
    public static async Task<ICommandResult> InvokeAsync<TCommand>(TCommand command, CommandContext context, params object[] arguments)
    {
        Assert.True(CommandHandlerReflection.TryGetValidatedHandlerMethod(typeof(TCommand), out var method, out _));
        var task = (Task)method!.Invoke(command, [context, .. arguments])!;
        await task;
        return (ICommandResult)task.GetType().GetProperty("Result")!.GetValue(task)!;
    }

    public static CommandContext CreateContext(
        string commandName,
        string trigger,
        string rawInputText,
        string? sourceMessageText = null,
        string executorDisplayName = "TestUser")
    {
        sourceMessageText ??= rawInputText;
        var sourceMessage = new ChannelChatMessage
        {
            BroadcasterUserId = "broadcaster-id",
            BroadcasterUserLogin = "broadcaster",
            BroadcasterUserName = "Broadcaster",
            ChatterUserId = "user-id",
            ChatterUserLogin = "user-login",
            ChatterUserName = executorDisplayName,
            MessageId = "message-id",
            Message = sourceMessageText is null
                ? null!
                : new ChatMessage
                {
                    Text = sourceMessageText,
                    Fragments = [new ChatMessageFragment { Type = "text", Text = sourceMessageText }]
                }
        };

        return new CommandContext(
            CommandInvocationKind.Chat,
            commandName,
            trigger,
            new CommandConfigurationSnapshot
            {
                Trigger = trigger,
                ChatTriggerMode = ChatTriggerMode.Chat,
                RewardTriggerMode = RewardTriggerMode.Disabled,
                RequiredAccessLevel = CommandAccessLevel.Everyone,
                CooldownScope = CommandCooldownScope.Global,
                BitsThreshold = 0,
                Cooldown = 0
            },
            sourceMessage,
            rawInputText,
            bits: 0,
            rewardId: null,
            CancellationToken.None,
            new FakeTwitchChatService());
    }
}

internal sealed class FakeYouTubeCommandWorkflow : IYouTubeCommandWorkflow
{
    public string? LastRequestedBy { get; private set; }
    public string? LastSourceMessage { get; private set; }
    public string? LastUrl { get; private set; }
    public int? LastVolume { get; private set; }
    public int SetRandomVolumeCallCount { get; private set; }
    public int NextCallCount { get; private set; }
    public int PreviousCallCount { get; private set; }
    public int GetCurrentSongCallCount { get; private set; }

    public YouTubeWorkflowResult<YouTubeSongInfo> CurrentSongResult { get; set; } =
        new(YouTubeWorkflowStatus.Success, new YouTubeSongInfo("Title", "Author", "Channel", "https://youtu.be/abc", "03:32"));

    public YouTubeWorkflowResult<YouTubeSongInfo> QueueSongResult { get; set; } =
        new(YouTubeWorkflowStatus.Success, new YouTubeSongInfo("Title", "Author", "Channel", "https://youtu.be/abc"));

    public YouTubeWorkflowResult<YouTubeSongInfo> StartSongResult { get; set; } =
        new(YouTubeWorkflowStatus.Success, new YouTubeSongInfo("Title", "Author", "Channel", "https://youtu.be/abc"));

    public YouTubeWorkflowResult<bool> NextResult { get; set; } = new(YouTubeWorkflowStatus.Success, true);
    public YouTubeWorkflowResult<bool> PreviousResult { get; set; } = new(YouTubeWorkflowStatus.Success, true);
    public YouTubeWorkflowResult<int> RandomVolumeResult { get; set; } = new(YouTubeWorkflowStatus.Success, 42);
    public YouTubeWorkflowResult<int> VolumeResult { get; set; } = new(YouTubeWorkflowStatus.Success, 42);

    public Task<YouTubeWorkflowResult<YouTubeSongInfo>> GetCurrentSongAsync()
    {
        GetCurrentSongCallCount++;
        return Task.FromResult(CurrentSongResult);
    }

    public Task<YouTubeWorkflowResult<YouTubeSongInfo>> QueueSongAsync(string requestedBy, string sourceMessage, string url)
    {
        LastRequestedBy = requestedBy;
        LastSourceMessage = sourceMessage;
        LastUrl = url;
        return Task.FromResult(QueueSongResult);
    }

    public Task<YouTubeWorkflowResult<YouTubeSongInfo>> StartSongAsync(string url)
    {
        LastUrl = url;
        return Task.FromResult(StartSongResult);
    }

    public Task<YouTubeWorkflowResult<bool>> NextAsync()
    {
        NextCallCount++;
        return Task.FromResult(NextResult);
    }

    public Task<YouTubeWorkflowResult<bool>> PreviousAsync()
    {
        PreviousCallCount++;
        return Task.FromResult(PreviousResult);
    }

    public Task<YouTubeWorkflowResult<int>> SetRandomVolumeAsync()
    {
        SetRandomVolumeCallCount++;
        return Task.FromResult(RandomVolumeResult);
    }

    public Task<YouTubeWorkflowResult<int>> SetVolumeAsync(int volume)
    {
        LastVolume = volume;
        return Task.FromResult(VolumeResult);
    }
}

internal sealed class FakeTwitchChatService : ITwitchChatService
{
    public Task ConnectAsync(string username, string accessToken, string channelLogin) => Task.CompletedTask;
    public Task DisconnectAsync() => Task.CompletedTask;
    public void SendMessage(ChannelChatMessage senderMessage, string message, bool asReply = true) { }
}
