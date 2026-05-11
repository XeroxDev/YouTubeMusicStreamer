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

using YouTubeMusicStreamer.Attributes;
using YouTubeMusicStreamer.Services.Commands;
using YouTubeMusicStreamer.Services.Commands.Binding;

namespace YouTubeMusicStreamer.Tests.Commands;

public class CommandHandlerReflectionTests
{
    [Fact]
    public void TryGetValidatedHandlerMethod_ReturnsError_WhenNoExecutionMethodExists()
    {
        var success = CommandHandlerReflection.TryGetValidatedHandlerMethod(typeof(NoExecutionHandler), out var method, out var error);

        Assert.False(success);
        Assert.Null(method);
        Assert.Equal("NoExecutionHandler is missing a method marked with [CommandExecution].", error);
    }

    [Fact]
    public void TryGetValidatedHandlerMethod_ReturnsError_WhenMultipleExecutionMethodsExist()
    {
        var success = CommandHandlerReflection.TryGetValidatedHandlerMethod(typeof(MultipleExecutionHandler), out var method, out var error);

        Assert.False(success);
        Assert.Null(method);
        Assert.Equal("MultipleExecutionHandler has multiple methods marked with [CommandExecution].", error);
    }

    [Fact]
    public void TryGetValidatedHandlerMethod_ReturnsError_WhenUnsupportedParameterTypeIsUsed()
    {
        var success = CommandHandlerReflection.TryGetValidatedHandlerMethod(typeof(UnsupportedParameterHandler), out var method, out var error);

        Assert.False(success);
        Assert.Null(method);
        Assert.Equal("UnsupportedParameterHandler.ExecuteAsync uses unsupported parameter type 'Decimal'.", error);
    }

    [Fact]
    public void TryGetValidatedHandlerMethod_ReturnsValidatedMethod_WhenSignatureIsSupported()
    {
        var success = CommandHandlerReflection.TryGetValidatedHandlerMethod(typeof(ValidHandler), out var method, out var error);

        Assert.True(success);
        Assert.NotNull(method);
        Assert.Null(error);
        Assert.Equal(nameof(ValidHandler.ExecuteAsync), method!.Name);
    }

    [Fact]
    public void TryGetValidatedHandlerMethod_ReturnsError_WhenHandlerDoesNotReturnTask()
    {
        var success = CommandHandlerReflection.TryGetValidatedHandlerMethod(typeof(NonTaskReturnHandler), out var method, out var error);

        Assert.False(success);
        Assert.Null(method);
        Assert.Equal("NonTaskReturnHandler.ExecuteAsync must return Task<CommandResult> or Task<CommandResult<TData>>.", error);
    }

    [Fact]
    public void TryGetValidatedHandlerMethod_ReturnsError_WhenTaskResultDoesNotImplementCommandResult()
    {
        var success = CommandHandlerReflection.TryGetValidatedHandlerMethod(typeof(WrongTaskResultHandler), out var method, out var error);

        Assert.False(success);
        Assert.Null(method);
        Assert.Equal("WrongTaskResultHandler.ExecuteAsync must return CommandResult or CommandResult<TData>.", error);
    }

    [Fact]
    public void TryGetValidatedHandlerMethod_ReturnsError_WhenFirstParameterIsNotCommandContext()
    {
        var success = CommandHandlerReflection.TryGetValidatedHandlerMethod(typeof(MissingContextHandler), out var method, out var error);

        Assert.False(success);
        Assert.Null(method);
        Assert.Equal("MissingContextHandler.ExecuteAsync must take CommandContext as its first parameter.", error);
    }

    [Fact]
    public void TryGetResultDataType_ReturnsNull_WhenHandlerReturnsNonGenericCommandResult()
    {
        var method = typeof(NonGenericResultHandler).GetMethod(nameof(NonGenericResultHandler.ExecuteAsync))!;

        var result = CommandHandlerReflection.TryGetResultDataType(method);

        Assert.Null(result);
    }

    [Fact]
    public void TryGetResultDataType_ReturnsGenericResultType_WhenHandlerReturnsCommandResultOfData()
    {
        var method = typeof(ValidHandler).GetMethod(nameof(ValidHandler.ExecuteAsync))!;

        var result = CommandHandlerReflection.TryGetResultDataType(method);

        Assert.Equal(typeof(string), result);
    }

    [Fact]
    public void TryGetResultDataType_ReturnsObject_WhenHandlerReturnsCommandResultOfObject()
    {
        var method = typeof(ObjectResultHandler).GetMethod(nameof(ObjectResultHandler.ExecuteAsync))!;

        var result = CommandHandlerReflection.TryGetResultDataType(method);

        Assert.Equal(typeof(object), result);
    }

    private sealed class NoExecutionHandler;

    private sealed class MultipleExecutionHandler
    {
        [CommandExecution]
        public Task<CommandResult> FirstAsync(CommandContext context) => Task.FromResult(CommandResult.Fulfilled());

        [CommandExecution]
        public Task<CommandResult> SecondAsync(CommandContext context) => Task.FromResult(CommandResult.Fulfilled());
    }

    private sealed class UnsupportedParameterHandler
    {
        [CommandExecution]
        public Task<CommandResult> ExecuteAsync(CommandContext context, decimal amount) => Task.FromResult(CommandResult.Fulfilled());
    }

    private sealed class NonTaskReturnHandler
    {
        [CommandExecution]
        public CommandResult ExecuteAsync(CommandContext context) => CommandResult.Fulfilled();
    }

    private sealed class WrongTaskResultHandler
    {
        [CommandExecution]
        public Task<string> ExecuteAsync(CommandContext context) => Task.FromResult("wrong");
    }

    private sealed class MissingContextHandler
    {
        [CommandExecution]
        public Task<CommandResult> ExecuteAsync(string value) => Task.FromResult(CommandResult.Fulfilled());
    }

    private sealed class ValidHandler
    {
        [CommandExecution]
        public Task<CommandResult<string>> ExecuteAsync(CommandContext context, int count, string name = "test") =>
            Task.FromResult(CommandResult<string>.Fulfilled($"{count}:{name}"));
    }

    private sealed class NonGenericResultHandler
    {
        [CommandExecution]
        public Task<CommandResult> ExecuteAsync(CommandContext context) => Task.FromResult(CommandResult.Fulfilled());
    }

    private sealed class ObjectResultHandler
    {
        [CommandExecution]
        public Task<CommandResult<object>> ExecuteAsync(CommandContext context) =>
            Task.FromResult(CommandResult<object>.Fulfilled(new object()));
    }
}
