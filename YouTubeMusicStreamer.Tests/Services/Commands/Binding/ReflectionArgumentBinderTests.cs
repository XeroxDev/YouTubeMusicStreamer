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

using System.Reflection;
using TwitchLib.EventSub.Core.Models.Chat;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;
using YouTubeMusicStreamer.Attributes;
using YouTubeMusicStreamer.Services.App.Persistence;
using YouTubeMusicStreamer.Services.Commands;
using YouTubeMusicStreamer.Services.Commands.Binding;
using YouTubeMusicStreamer.Services.Twitch.Interfaces;

namespace YouTubeMusicStreamer.Tests.Commands;

public class ReflectionArgumentBinderTests
{
    private readonly ReflectionArgumentBinder _binder = new();

    [Fact]
    public async Task BindAndInvokeAsync_BindsPrimitiveArguments_AndRestOfInput()
    {
        var handler = new BinderHandler();
        var method = typeof(BinderHandler).GetMethod(nameof(BinderHandler.ExecuteAsync), BindingFlags.Instance | BindingFlags.Public)!;
        var context = CreateContext();

        var result = await _binder.BindAndInvokeAsync(handler, method, context, ["7", "2.5", "rest", "of", "input"]);

        Assert.Equal(CommandExecutionStatus.Fulfilled, result.Result.Status);
        Assert.Equal("7", result.ArgValues["{count}"]);
        Assert.Equal("2.5", result.ArgValues["{ratio}"]);
        Assert.Equal("rest of input", result.ArgValues["{tail}"]);
        Assert.Equal((7, 2.5, "rest of input"), handler.LastInvocation);
    }

    [Fact]
    public async Task BindAndInvokeAsync_ReturnsMissingArgument_WhenRequiredArgumentIsAbsent()
    {
        var handler = new BinderHandler();
        var method = typeof(BinderHandler).GetMethod(nameof(BinderHandler.ExecuteAsync), BindingFlags.Instance | BindingFlags.Public)!;

        var result = await _binder.BindAndInvokeAsync(handler, method, CreateContext(), []);

        Assert.Equal(CommandExecutionStatus.BadInput, result.Result.Status);
        Assert.Equal(CommandExecutionReason.MissingArgument, result.Result.Reason);
        Assert.Equal("Missing 'count'.", result.Result.Message);
    }

    [Fact]
    public async Task BindAndInvokeAsync_ReturnsInvalidArgument_WhenConversionFails()
    {
        var handler = new BinderHandler();
        var method = typeof(BinderHandler).GetMethod(nameof(BinderHandler.ExecuteAsync), BindingFlags.Instance | BindingFlags.Public)!;

        var result = await _binder.BindAndInvokeAsync(handler, method, CreateContext(), ["abc"]);

        Assert.Equal(CommandExecutionStatus.BadInput, result.Result.Status);
        Assert.Equal(CommandExecutionReason.InvalidArgument, result.Result.Reason);
        Assert.Equal("'abc' is not a valid value for 'count'.", result.Result.Message);
        Assert.Equal("abc", result.ArgValues["{count}"]);
    }

    [Fact]
    public async Task BindAndInvokeAsync_UsesParameterDefaultValue_WhenAvailable()
    {
        var handler = new DefaultValueHandler();
        var method = typeof(DefaultValueHandler).GetMethod(nameof(DefaultValueHandler.ExecuteAsync), BindingFlags.Instance | BindingFlags.Public)!;

        var result = await _binder.BindAndInvokeAsync(handler, method, CreateContext(), []);

        Assert.Equal(CommandExecutionStatus.Fulfilled, result.Result.Status);
        Assert.Equal("fallback", result.ArgValues["{text}"]);
        Assert.Equal("fallback", handler.LastText);
    }

    [Fact]
    public async Task BindAndInvokeAsync_UsesDefaultValue_ForRestOfInputParameter_WhenNoTokensRemain()
    {
        var handler = new RestDefaultHandler();
        var method = typeof(RestDefaultHandler).GetMethod(nameof(RestDefaultHandler.ExecuteAsync), BindingFlags.Instance | BindingFlags.Public)!;

        var result = await _binder.BindAndInvokeAsync(handler, method, CreateContext(), []);

        Assert.Equal(CommandExecutionStatus.Fulfilled, result.Result.Status);
        Assert.Equal("fallback tail", result.ArgValues["{tail}"]);
        Assert.Equal("fallback tail", handler.LastTail);
    }

    [Fact]
    public async Task BindAndInvokeAsync_ReturnsMissingArgument_WhenRestOfInputParameterHasNoDefaultAndNoTokensRemain()
    {
        var handler = new RequiredRestHandler();
        var method = typeof(RequiredRestHandler).GetMethod(nameof(RequiredRestHandler.ExecuteAsync), BindingFlags.Instance | BindingFlags.Public)!;

        var result = await _binder.BindAndInvokeAsync(handler, method, CreateContext(), []);

        Assert.Equal(CommandExecutionStatus.BadInput, result.Result.Status);
        Assert.Equal(CommandExecutionReason.MissingArgument, result.Result.Reason);
        Assert.Equal("Missing 'tail'.", result.Result.Message);
    }

    [Fact]
    public async Task BindAndInvokeAsync_RejectsInvalidInvariantDoubleFormats()
    {
        var handler = new BinderHandler();
        var method = typeof(BinderHandler).GetMethod(nameof(BinderHandler.ExecuteAsync), BindingFlags.Instance | BindingFlags.Public)!;

        var result = await _binder.BindAndInvokeAsync(handler, method, CreateContext(), ["7", "2,5"]);

        Assert.Equal(CommandExecutionStatus.BadInput, result.Result.Status);
        Assert.Equal(CommandExecutionReason.InvalidArgument, result.Result.Reason);
        Assert.Equal("'2,5' is not a valid value for 'ratio'.", result.Result.Message);
        Assert.Equal("7", result.ArgValues["{count}"]);
        Assert.Equal("2,5", result.ArgValues["{ratio}"]);
    }

    [Fact]
    public async Task BindAndInvokeAsync_ReturnsInvalidArgument_WhenIntegerOverflows()
    {
        var handler = new BinderHandler();
        var method = typeof(BinderHandler).GetMethod(nameof(BinderHandler.ExecuteAsync), BindingFlags.Instance | BindingFlags.Public)!;

        var result = await _binder.BindAndInvokeAsync(handler, method, CreateContext(), ["999999999999"]);

        Assert.Equal(CommandExecutionStatus.BadInput, result.Result.Status);
        Assert.Equal(CommandExecutionReason.InvalidArgument, result.Result.Reason);
        Assert.Equal("'999999999999' is not a valid value for 'count'.", result.Result.Message);
    }

    [Fact]
    public async Task BindAndInvokeAsync_KeepsGlobalPlaceholders_WhenBindingFailsEarly()
    {
        var handler = new BinderHandler();
        var method = typeof(BinderHandler).GetMethod(nameof(BinderHandler.ExecuteAsync), BindingFlags.Instance | BindingFlags.Public)!;

        var result = await _binder.BindAndInvokeAsync(handler, method, CreateContext(), []);

        Assert.Equal("User", result.ArgValues["{username}"]);
        Assert.Equal("123", result.ArgValues["{bits}"]);
    }

    [Fact]
    public async Task BindAndInvokeAsync_AcceptsSignedNumericInputs()
    {
        var handler = new BinderHandler();
        var method = typeof(BinderHandler).GetMethod(nameof(BinderHandler.ExecuteAsync), BindingFlags.Instance | BindingFlags.Public)!;

        var result = await _binder.BindAndInvokeAsync(handler, method, CreateContext(), ["-7", "-2.5", "tail"]);

        Assert.Equal(CommandExecutionStatus.Fulfilled, result.Result.Status);
        Assert.Equal("-7", result.ArgValues["{count}"]);
        Assert.Equal("-2.5", result.ArgValues["{ratio}"]);
        Assert.Equal((-7, -2.5, "tail"), handler.LastInvocation);
    }

    [Fact]
    public async Task BindAndInvokeAsync_IgnoresExtraTokens_WhenHandlerHasNoRestOfInputParameter()
    {
        var handler = new FixedArityHandler();
        var method = typeof(FixedArityHandler).GetMethod(nameof(FixedArityHandler.ExecuteAsync), BindingFlags.Instance | BindingFlags.Public)!;

        var result = await _binder.BindAndInvokeAsync(handler, method, CreateContext(), ["7", "extra", "tokens"]);

        Assert.Equal(CommandExecutionStatus.Fulfilled, result.Result.Status);
        Assert.Equal("7", result.ArgValues["{count}"]);
        Assert.Equal(7, handler.LastCount);
    }

    private static CommandContext CreateContext() =>
        new(
            CommandInvocationKind.Chat,
            "request",
            "request",
            new CommandConfigurationSnapshot
            {
                Trigger = "request",
                ChatTriggerMode = ChatTriggerMode.Chat,
                RewardTriggerMode = RewardTriggerMode.Disabled,
                RequiredAccessLevel = CommandAccessLevel.Everyone,
                CooldownScope = CommandCooldownScope.Global,
                BitsThreshold = 0,
                Cooldown = 0
            },
            new ChannelChatMessage
            {
                BroadcasterUserId = "broadcaster-id",
                BroadcasterUserLogin = "broadcaster",
                BroadcasterUserName = "Broadcaster",
                ChatterUserId = "user-id",
                ChatterUserLogin = "user-login",
                ChatterUserName = "User",
                Message = new ChatMessage
                {
                    Text = "!request song",
                    Fragments =
                    [
                        new ChatMessageFragment { Type = "text", Text = "!request song" }
                    ]
                }
            },
            "!request song",
            123,
            null,
            CancellationToken.None,
            new FakeTwitchChatService());

    private sealed class FakeTwitchChatService : ITwitchChatService
    {
        public Task ConnectAsync(string username, string accessToken, string channelLogin) => Task.CompletedTask;
        public Task DisconnectAsync() => Task.CompletedTask;
        public void SendMessage(ChannelChatMessage senderMessage, string message, bool asReply = true) { }
    }

    private sealed class BinderHandler
    {
        public (int Count, double Ratio, string Tail) LastInvocation { get; private set; }

        [CommandExecution]
        public Task<CommandResult> ExecuteAsync(CommandContext context, int count, double ratio = 1.5, [CommandArgument(RestOfInput = true)] string tail = "")
        {
            LastInvocation = (count, ratio, tail);
            return Task.FromResult(CommandResult.Fulfilled());
        }
    }

    private sealed class DefaultValueHandler
    {
        public string? LastText { get; private set; }

        [CommandExecution]
        public Task<CommandResult> ExecuteAsync(CommandContext context, string text = "fallback")
        {
            LastText = text;
            return Task.FromResult(CommandResult.Fulfilled());
        }
    }

    private sealed class RestDefaultHandler
    {
        public string? LastTail { get; private set; }

        [CommandExecution]
        public Task<CommandResult> ExecuteAsync(CommandContext context, [CommandArgument(RestOfInput = true)] string tail = "fallback tail")
        {
            LastTail = tail;
            return Task.FromResult(CommandResult.Fulfilled());
        }
    }

    private sealed class RequiredRestHandler
    {
        [CommandExecution]
        public Task<CommandResult> ExecuteAsync(CommandContext context, [CommandArgument(RestOfInput = true)] string tail) =>
            Task.FromResult(CommandResult.Fulfilled());
    }

    private sealed class FixedArityHandler
    {
        public int LastCount { get; private set; }

        [CommandExecution]
        public Task<CommandResult> ExecuteAsync(CommandContext context, int count)
        {
            LastCount = count;
            return Task.FromResult(CommandResult.Fulfilled());
        }
    }
}
