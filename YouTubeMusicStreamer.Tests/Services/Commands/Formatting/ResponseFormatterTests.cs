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

using YouTubeMusicStreamer.Services.Commands.Formatting;

namespace YouTubeMusicStreamer.Tests.Commands;

public class ResponseFormatterTests
{
    private readonly ResponseFormatter _formatter = new();

    [Fact]
    public void Format_ReturnsEmpty_ForBlankTemplate()
    {
        var result = _formatter.Format("   ", new Dictionary<string, string>());

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Format_ReplacesKnownPlaceholders()
    {
        var result = _formatter.Format(
            "Now playing {title} by {author}",
            new Dictionary<string, string>
            {
                ["{title}"] = "Song",
                ["{author}"] = "Artist"
            });

        Assert.Equal("Now playing Song by Artist", result);
    }

    [Fact]
    public void Format_LeavesUnknownPlaceholdersUntouched()
    {
        var result = _formatter.Format(
            "Hello {username} from {planet}",
            new Dictionary<string, string>
            {
                ["{username}"] = "TestUser"
            });

        Assert.Equal("Hello TestUser from {planet}", result);
    }

    [Fact]
    public void Format_ReplacesRepeatedPlaceholdersEverywhere()
    {
        var result = _formatter.Format(
            "{username} greeted {username}",
            new Dictionary<string, string>
            {
                ["{username}"] = "TestUser"
            });

        Assert.Equal("TestUser greeted TestUser", result);
    }

    [Fact]
    public void Format_ReturnsTemplateUnchanged_WhenValuesAreEmpty()
    {
        var result = _formatter.Format("Hello {username}", new Dictionary<string, string>());

        Assert.Equal("Hello {username}", result);
    }

    [Fact]
    public void Format_LeavesCaseMismatchedPlaceholdersUntouched()
    {
        var result = _formatter.Format(
            "Hello {Username}",
            new Dictionary<string, string>
            {
                ["{username}"] = "TestUser"
            });

        Assert.Equal("Hello {Username}", result);
    }

    [Fact]
    public void Format_DoesNotRecursivelyExpandPlaceholderSyntax_InsideReplacementValues()
    {
        var result = _formatter.Format(
            "Value: {title}",
            new Dictionary<string, string>
            {
                ["{title}"] = "{author}",
                ["{author}"] = "Artist"
            });

        Assert.Equal("Value: {author}", result);
    }
}
