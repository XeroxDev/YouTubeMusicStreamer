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
using YouTubeMusicStreamer.Attributes;
using YouTubeMusicStreamer.Services.Commands;
using YouTubeMusicStreamer.Services.Commands.Placeholders;

namespace YouTubeMusicStreamer.Tests.Services.Commands.Placeholders;

public class ReflectionPlaceholderProviderTests
{
    private readonly ReflectionPlaceholderProvider _provider = new();

    [Fact]
    public void GetPlaceholders_ReturnsGlobalArgumentAndResultPlaceholders_WhenHandlerReturnsResultData()
    {
        var method = typeof(HandlerWithResultData).GetMethod(nameof(HandlerWithResultData.ExecuteAsync), BindingFlags.Instance | BindingFlags.Public);

        var placeholders = _provider.GetPlaceholders(typeof(HandlerWithResultData), method!);

        Assert.Equal("The name of the user invoking the command", placeholders["{username}"]);
        Assert.Equal("The total number of bits the user cheered", placeholders["{bits}"]);
        Assert.Equal("Requested song title", placeholders["{title}"]);
        Assert.Equal("Command argument 'count'", placeholders["{count}"]);
        Assert.Equal("Queued video title", placeholders["{videotitle}"]);
        Assert.Equal("Result field 'DurationSeconds'", placeholders["{durationseconds}"]);
    }

    [Fact]
    public void GetPlaceholders_ReturnsOnlyGlobalAndArgumentPlaceholders_WhenHandlerReturnsNonGenericResult()
    {
        var method = typeof(HandlerWithoutResultData).GetMethod(nameof(HandlerWithoutResultData.ExecuteAsync), BindingFlags.Instance | BindingFlags.Public);

        var placeholders = _provider.GetPlaceholders(typeof(HandlerWithoutResultData), method!);

        Assert.Contains("{username}", placeholders.Keys);
        Assert.Contains("{bits}", placeholders.Keys);
        Assert.Contains("{query}", placeholders.Keys);
        Assert.DoesNotContain("{videotitle}", placeholders.Keys);
    }

    [Fact]
    public void GetPlaceholders_ReturnsLowercaseKeys_WhenParameterNamesUseMixedCase()
    {
        var method = typeof(HandlerWithMixedCaseParameters).GetMethod(nameof(HandlerWithMixedCaseParameters.ExecuteAsync), BindingFlags.Instance | BindingFlags.Public);

        var placeholders = _provider.GetPlaceholders(typeof(HandlerWithMixedCaseParameters), method!);

        Assert.Contains("{userid}", placeholders.Keys);
        Assert.Contains("{displayname}", placeholders.Keys);
    }

    [Fact]
    public void GetPlaceholders_OverridesGlobalDescription_WhenArgumentUsesSamePlaceholderKey()
    {
        var method = typeof(HandlerWithUsernameArgument).GetMethod(nameof(HandlerWithUsernameArgument.ExecuteAsync), BindingFlags.Instance | BindingFlags.Public);

        var placeholders = _provider.GetPlaceholders(typeof(HandlerWithUsernameArgument), method!);

        Assert.Equal("Supplied target username", placeholders["{username}"]);
    }

    [Fact]
    public void GetPlaceholders_OverridesArgumentDescription_WhenResultDataUsesSamePlaceholderKey()
    {
        var method = typeof(HandlerWithCollidingResultData).GetMethod(nameof(HandlerWithCollidingResultData.ExecuteAsync), BindingFlags.Instance | BindingFlags.Public);

        var placeholders = _provider.GetPlaceholders(typeof(HandlerWithCollidingResultData), method!);

        Assert.Equal("Resolved user name from result data", placeholders["{username}"]);
    }

    [Fact]
    public void GetPlaceholders_DoesNotReflectResultProperties_WhenHandlerReturnsCommandResultOfObject()
    {
        var method = typeof(HandlerWithObjectResult).GetMethod(nameof(HandlerWithObjectResult.ExecuteAsync), BindingFlags.Instance | BindingFlags.Public);

        var placeholders = _provider.GetPlaceholders(typeof(HandlerWithObjectResult), method!);

        Assert.Contains("{query}", placeholders.Keys);
        Assert.DoesNotContain("{username}", placeholders.Where(x => x.Value == "Result field 'Username'").Select(x => x.Key));
        Assert.DoesNotContain("{score}", placeholders.Keys);
    }

    private sealed class HandlerWithResultData
    {
        public Task<CommandResult<ResultData>> ExecuteAsync(
            CommandContext context,
            [Placeholder("Requested song title")] string title,
            int count) =>
            Task.FromResult(CommandResult<ResultData>.Fulfilled(new ResultData()));
    }

    private sealed class HandlerWithoutResultData
    {
        public Task<CommandResult> ExecuteAsync(CommandContext context, string query) =>
            Task.FromResult(CommandResult.Fulfilled());
    }

    private sealed class HandlerWithMixedCaseParameters
    {
        public Task<CommandResult> ExecuteAsync(CommandContext context, string UserId, string DisplayName) =>
            Task.FromResult(CommandResult.Fulfilled());
    }

    private sealed class HandlerWithUsernameArgument
    {
        public Task<CommandResult> ExecuteAsync(
            CommandContext context,
            [Placeholder("Supplied target username")] string username) =>
            Task.FromResult(CommandResult.Fulfilled());
    }

    private sealed class HandlerWithCollidingResultData
    {
        public Task<CommandResult<CollidingResultData>> ExecuteAsync(
            CommandContext context,
            [Placeholder("Argument username")] string username) =>
            Task.FromResult(CommandResult<CollidingResultData>.Fulfilled(new CollidingResultData()));
    }

    private sealed class HandlerWithObjectResult
    {
        public Task<CommandResult<object>> ExecuteAsync(CommandContext context, string query) =>
            Task.FromResult(CommandResult<object>.Fulfilled(new { Username = "TestUser", Score = 42 }));
    }

    private sealed class ResultData
    {
        [Placeholder("Queued video title")]
        public string VideoTitle { get; init; } = "Track";

        public int DurationSeconds { get; init; } = 120;
    }

    private sealed class CollidingResultData
    {
        [Placeholder("Resolved user name from result data")]
        public string Username { get; init; } = "TestUser";
    }
}
