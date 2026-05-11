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

using YouTubeMusicStreamer.Commands;
using YouTubeMusicStreamer.Services.Commands;
using YouTubeMusicStreamer.Tests.TestSupport;

namespace YouTubeMusicStreamer.Tests.Commands;

public sealed class ListCommandTests
{
    [Fact]
    public async Task ExecuteCommandLogicAsync_JoinsEnabledCommands_WithSemicolonSeparator()
    {
        var catalog = new FakeCommandCatalog(["!request <url>", "!next", "!volume <0-100>"]);
        var command = new ListCommand(catalog);

        var outcome = await CommandTestSupport.InvokeAsync(command, CommandTestSupport.CreateContext("ListCommand", "list", "!list"));

        Assert.Equal(CommandExecutionStatus.Fulfilled, outcome.Status);
        var result = Assert.IsType<ListCommand.ListResult>(outcome.Data);
        Assert.Equal("!request <url>; !next; !volume <0-100>", result.Commands);
        Assert.Equal(1, catalog.EnabledListingCallCount);
    }

    [Fact]
    public async Task ExecuteCommandLogicAsync_UsesFallbackText_WhenNoCommandsAreEnabled()
    {
        var catalog = new FakeCommandCatalog();
        var command = new ListCommand(catalog);

        var outcome = await CommandTestSupport.InvokeAsync(command, CommandTestSupport.CreateContext("ListCommand", "list", "!list"));

        Assert.Equal(CommandExecutionStatus.Fulfilled, outcome.Status);
        var result = Assert.IsType<ListCommand.ListResult>(outcome.Data);
        Assert.Equal("No commands are currently enabled.", result.Commands);
        Assert.Equal(1, catalog.EnabledListingCallCount);
    }

    private sealed class FakeCommandCatalog : ICommandCatalog
    {
        private readonly IReadOnlyList<string> _enabledCommandListing;

        public FakeCommandCatalog(IReadOnlyList<string>? enabledCommandListing = null)
        {
            _enabledCommandListing = enabledCommandListing ?? [];
        }

        public int EnabledListingCallCount { get; private set; }

        public IReadOnlyList<CommandDescriptor> GetCommandDescriptors() => [];

        public IReadOnlyList<string> GetEnabledCommandListing()
        {
            EnabledListingCallCount++;
            return _enabledCommandListing;
        }
    }
}
