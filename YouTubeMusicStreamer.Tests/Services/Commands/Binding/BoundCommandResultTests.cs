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

using YouTubeMusicStreamer.Services.Commands;
using YouTubeMusicStreamer.Services.Commands.Binding;

namespace YouTubeMusicStreamer.Tests.Services.Commands.Binding;

public sealed class BoundCommandResultTests
{
    [Fact]
    public void Constructor_PreservesResultReference()
    {
        var result = CommandResult.Fulfilled("ok");

        var bound = new BoundCommandResult(result, new Dictionary<string, string>());

        Assert.Same(result, bound.Result);
    }

    [Fact]
    public void Constructor_SnapshotsArgumentValues()
    {
        var args = new Dictionary<string, string>
        {
            ["url"] = "https://youtu.be/abc",
            ["reason"] = "first"
        };

        var bound = new BoundCommandResult(CommandResult.Fulfilled(), args);
        args["reason"] = "mutated";
        args["extra"] = "late";

        Assert.Equal("first", bound.ArgValues["reason"]);
        Assert.False(bound.ArgValues.ContainsKey("extra"));
    }
}
