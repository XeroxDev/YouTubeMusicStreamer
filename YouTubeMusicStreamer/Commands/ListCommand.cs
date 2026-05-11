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

using JetBrains.Annotations;
using YouTubeMusicStreamer.Attributes;
using YouTubeMusicStreamer.Services.Commands;

namespace YouTubeMusicStreamer.Commands;

[Command("Lists all enabled commands.", true, 30, "Available commands: {commands}")]
public sealed class ListCommand(ICommandCatalog commandCatalog)
{
    [CommandExecution]
    [UsedImplicitly]
    private Task<CommandResult<ListResult>> ExecuteCommandLogicAsync(CommandContext context)
    {
        var commands = string.Join("; ", commandCatalog.GetEnabledCommandListing());
        return Task.FromResult(CommandResult<ListResult>.Fulfilled(new ListResult
        {
            Commands = string.IsNullOrWhiteSpace(commands) ? "No commands are currently enabled." : commands
        }));
    }

    public sealed class ListResult
    {
        [Placeholder("The enabled commands and their configured invocation shapes.")]
        public string Commands { get; init; } = string.Empty;
    }
}
