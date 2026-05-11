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
using YouTubeMusicStreamer.Services.Commands.ArgumentParser;

namespace YouTubeMusicStreamer.Tests.Commands;

public class ArgumentParserTests
{
    private readonly ArgumentParser _parser = new();

    [Fact]
    public void Parse_ReturnsEmpty_WhenMessageIsNull()
    {
        var message = new ChannelChatMessage();

        var result = _parser.Parse(message, "!", "request");

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_StripsPrefixAndTrigger_AndPreservesQuotedArguments()
    {
        var message = CreateMessage("""!request "hello world" 42""");

        var result = _parser.Parse(message, "!", "request");

        Assert.Equal(["hello world", "42"], result);
    }

    [Fact]
    public void Parse_ReturnsTokensFromTextFragmentsOnly_WhenMessageContainsMixedFragmentTypes()
    {
        var message = new ChannelChatMessage
        {
            Message = new ChatMessage
            {
                Text = "!request song",
                Fragments =
                [
                    new ChatMessageFragment { Type = "text", Text = "!request " },
                    new ChatMessageFragment { Type = "mention", Text = "@someone" },
                    new ChatMessageFragment { Type = "text", Text = "song" }
                ]
            }
        };

        var result = _parser.Parse(message, "!", "request");

        Assert.Equal(["song"], result);
    }

    [Fact]
    public void Parse_ReturnsEmpty_WhenOnlyNonTextFragmentsExist()
    {
        var message = new ChannelChatMessage
        {
            Message = new ChatMessage
            {
                Text = "!request song",
                Fragments =
                [
                    new ChatMessageFragment { Type = "mention", Text = "@someone" }
                ]
            }
        };

        var result = _parser.Parse(message, "!", "request");

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_RemovesPrefixAndTrigger_CaseInsensitively_AndTrimsLeadingWhitespace()
    {
        var message = CreateMessage("   !ReQuEsT   song  next ");

        var result = _parser.Parse(message, "!", "request");

        Assert.Equal(["song", "next"], result);
    }

    [Fact]
    public void Parse_LeavesTextUntouched_WhenPrefixAndTriggerDoNotMatch()
    {
        var message = CreateMessage("hello there");

        var result = _parser.Parse(message, "!", "request");

        Assert.Equal(["hello", "there"], result);
    }

    [Fact]
    public void Parse_SplitsUnclosedQuotedInput_AccordingToCurrentTokenizerBehavior()
    {
        var message = CreateMessage("""!request "hello world""");

        var result = _parser.Parse(message, "!", "request");

        Assert.Equal(["hello", "world"], result);
    }

    [Fact]
    public void Parse_ReturnsArguments_WhenPrefixContainsMultipleCharacters()
    {
        var message = CreateMessage("??request song");

        var result = _parser.Parse(message, "??", "request");

        Assert.Equal(["song"], result);
    }

    [Fact]
    public void Parse_ReturnsArguments_WhenPrefixContainsSpaces()
    {
        var message = CreateMessage("hey bot request song");

        var result = _parser.Parse(message, "hey bot ", "request");

        Assert.Equal(["song"], result);
    }

    [Fact]
    public void Parse_ReturnsArguments_WhenMultiWordPrefixUsesOddCasing()
    {
        var message = CreateMessage("HeY BoT request song");

        var result = _parser.Parse(message, "hey bot ", "request");

        Assert.Equal(["song"], result);
    }

    [Fact]
    public void Parse_ReturnsCollapsedTokens_WhenInputContainsTabsAndRepeatedWhitespace()
    {
        var message = CreateMessage("!request\t\talpha   \tbeta");

        var result = _parser.Parse(message, "!", "request");

        Assert.Equal(["alpha", "beta"], result);
    }

    [Fact]
    public void Parse_PreservesEmptyQuotedArguments()
    {
        var message = CreateMessage("!request \"\" \"hello world\"");

        var result = _parser.Parse(message, "!", "request");

        Assert.Equal(["", "hello world"], result);
    }

    [Fact]
    public void Parse_ReturnsCombinedTokenization_WhenCommandTextIsSplitAcrossFragments()
    {
        var message = new ChannelChatMessage
        {
            Message = new ChatMessage
            {
                Text = "!request song name",
                Fragments =
                [
                    new ChatMessageFragment { Type = "text", Text = "!req" },
                    new ChatMessageFragment { Type = "text", Text = "uest " },
                    new ChatMessageFragment { Type = "text", Text = "\"song name\"" }
                ]
            }
        };

        var result = _parser.Parse(message, "!", "request");

        Assert.Equal(["song name"], result);
    }

    [Fact]
    public void Parse_ReturnsConcatenatedToken_WhenFragmentsJoinWithoutSpacing()
    {
        var message = new ChannelChatMessage
        {
            Message = new ChatMessage
            {
                Text = "!request songname",
                Fragments =
                [
                    new ChatMessageFragment { Type = "text", Text = "!request song" },
                    new ChatMessageFragment { Type = "text", Text = "name" }
                ]
            }
        };

        var result = _parser.Parse(message, "!", "request");

        Assert.Equal(["songname"], result);
    }

    [Fact]
    public void Parse_IgnoresLeadingNonTextFragments_BeforeActualCommandText()
    {
        var message = new ChannelChatMessage
        {
            Message = new ChatMessage
            {
                Text = "!request song",
                Fragments =
                [
                    new ChatMessageFragment { Type = "mention", Text = "@streamer" },
                    new ChatMessageFragment { Type = "text", Text = "!request song" }
                ]
            }
        };

        var result = _parser.Parse(message, "!", "request");

        Assert.Equal(["song"], result);
    }

    [Fact]
    public void Parse_IgnoresLeadingCheermoteFragments_BeforeActualCommandText()
    {
        var message = new ChannelChatMessage
        {
            Message = new ChatMessage
            {
                Text = "cheer100 !request song",
                Fragments =
                [
                    CreateCheermoteFragment(100),
                    new ChatMessageFragment { Type = "text", Text = " !request song" }
                ]
            }
        };

        var result = _parser.Parse(message, "!", "request");

        Assert.Equal(["song"], result);
    }

    [Fact]
    public void Parse_ReturnsArguments_WhenCheermoteFragmentsAreSprinkledBetweenCommandTokens()
    {
        var message = new ChannelChatMessage
        {
            Message = new ChatMessage
            {
                Text = "!request first cheer100 second cheer25 third",
                Fragments =
                [
                    new ChatMessageFragment { Type = "text", Text = "!request first " },
                    CreateCheermoteFragment(100),
                    new ChatMessageFragment { Type = "text", Text = " second " },
                    CreateCheermoteFragment(25),
                    new ChatMessageFragment { Type = "text", Text = " third" }
                ]
            }
        };

        var result = _parser.Parse(message, "!", "request");

        Assert.Equal(["first", "second", "third"], result);
    }

    [Fact]
    public void Parse_ReturnsArguments_WhenCheermoteFragmentSplitsTriggerText()
    {
        var message = new ChannelChatMessage
        {
            Message = new ChatMessage
            {
                Text = "!request song",
                Fragments =
                [
                    new ChatMessageFragment { Type = "text", Text = "!req" },
                    CreateCheermoteFragment(100),
                    new ChatMessageFragment { Type = "text", Text = "uest song" }
                ]
            }
        };

        var result = _parser.Parse(message, "!", "request");

        Assert.Equal(["song"], result);
    }

    [Fact]
    public void Parse_ReturnsMergedToken_WhenCheermoteFragmentsRemoveInternalSpacing()
    {
        var message = new ChannelChatMessage
        {
            Message = new ChatMessage
            {
                Text = "!request songcheer100name",
                Fragments =
                [
                    new ChatMessageFragment { Type = "text", Text = "!request song" },
                    CreateCheermoteFragment(100),
                    new ChatMessageFragment { Type = "text", Text = "name" }
                ]
            }
        };

        var result = _parser.Parse(message, "!", "request");

        Assert.Equal(["songname"], result);
    }

    [Fact]
    public void Parse_ReturnsEmpty_WhenOnlyTriggerWasProvided()
    {
        var message = CreateMessage("!request   ");

        var result = _parser.Parse(message, "!", "request");

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_StripsPartialTriggerPrefix_WhenInputStartsWithLongerTriggerText()
    {
        var message = CreateMessage("!request song");

        var result = _parser.Parse(message, "!", "req");

        Assert.Equal(["uest", "song"], result);
    }

    [Fact]
    public void Parse_HandlesPathologicalQuoteBombing_WithoutCrashing()
    {
        var input = "!request " + string.Concat(Enumerable.Repeat("\"\"", 1000));
        var message = CreateMessage(input);

        var result = _parser.Parse(message, "!", "request");

        Assert.Equal(1000, result.Count);
        Assert.All(result, Assert.Empty);
    }

    [Fact]
    public void Parse_HandlesExtremelyLongInputs_Gracefully()
    {
        var input = "!request " + new string('a', 10000);
        var message = CreateMessage(input);

        var result = _parser.Parse(message, "!", "request");

        var arg = Assert.Single(result);
        Assert.Equal(10000, arg.Length);
    }

    [Fact]
    public void Parse_HandlesMassiveWhitespace_ByCollapsingTokens()
    {
        var input = "!request" + new string(' ', 500) + "token1" + new string('\t', 500) + "token2";
        var message = CreateMessage(input);

        var result = _parser.Parse(message, "!", "request");

        Assert.Equal(["token1", "token2"], result);
    }

    [Fact]
    public void Parse_HandlesUnbalancedQuotesMixedWithFragments_Consistently()
    {
        var message = new ChannelChatMessage
        {
            Message = new ChatMessage
            {
                Text = "!request \"unbalanced fragments",
                Fragments =
                [
                    new ChatMessageFragment { Type = "text", Text = "!req" },
                    new ChatMessageFragment { Type = "text", Text = "uest \"" },
                    new ChatMessageFragment { Type = "mention", Text = "@someone" },
                    new ChatMessageFragment { Type = "text", Text = " unbalanced fragments" }
                ]
            }
        };

        var result = _parser.Parse(message, "!", "request");

        // According to TokenRegex: "[^"]*"|\S+
        // After stripping "!request ", we have: " unbalanced fragments
        // " is matched by "[^"]*", trimmed to ""
        // unbalanced is \S+, fragments is \S+
        Assert.Equal(["", "unbalanced", "fragments"], result);
    }

    private static ChannelChatMessage CreateMessage(string text) =>
        new()
        {
            Message = new ChatMessage
            {
                Text = text,
                Fragments =
                [
                    new ChatMessageFragment { Type = "text", Text = text }
                ]
            }
        };

    private static ChatMessageFragment CreateCheermoteFragment(int bits) =>
        new()
        {
            Type = "cheermote",
            Text = $"cheer{bits}",
            Cheermote = new()
            {
                Prefix = "cheer",
                Bits = bits,
                Tier = 1
            }
        };
}
