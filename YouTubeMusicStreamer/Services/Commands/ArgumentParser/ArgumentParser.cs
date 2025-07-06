// This file is part of YouTubeMusicStreamer.
// Copyright (C) 2025 Dominic Ris
// 
// YouTubeMusicStreamer is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as published
// by the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version (the "AGPLv3").
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

using System.Text.RegularExpressions;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;

namespace YouTubeMusicStreamer.Services.Commands.ArgumentParser;

public partial class ArgumentParser : IArgumentParser
{
    public IReadOnlyList<string> Parse(ChannelChatMessage msg, string prefix, string trigger)
    {
        var raw = string.Concat(
            msg.Message.Fragments
                .Where(f => f.Type == "text")
                .Select(f => f.Text)
        ).Trim();

        var tokens = TokenRegex().Matches(raw)
            .Select(m => m.Value.Trim('"'))
            .ToList();

        if (tokens.FirstOrDefault()?.Equals(prefix, StringComparison.OrdinalIgnoreCase) == true)
            tokens.RemoveAt(0);
        if (tokens.FirstOrDefault()?.Equals(trigger, StringComparison.OrdinalIgnoreCase) == true)
            tokens.RemoveAt(0);

        return tokens;
    }

    [GeneratedRegex("""
                    "[^"]+"|\S+
                    """, RegexOptions.Compiled)]
    private static partial Regex TokenRegex();
}