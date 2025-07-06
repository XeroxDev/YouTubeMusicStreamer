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

using System.Reflection;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;
using YouTubeMusicStreamer.Attributes;
using YouTubeMusicStreamer.Interfaces;

namespace YouTubeMusicStreamer.Services.Commands.Placeholders;

public class ReflectionPlaceholderProvider : IPlaceholderProvider
{
    private static readonly Dictionary<string, string> Global = new()
    {
        ["{username}"] = "The name of the user invoking the command",
        ["{bits}"] = "The total number of bits the user cheered"
    };

    public IReadOnlyDictionary<string, string> GetPlaceholders(ICommand handler)
    {
        var map = new Dictionary<string, string>(Global);

        var type = handler.GetType();
        var method = type.GetMethod("ExecuteCommandLogicAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        if (method is null) return map;

        foreach (var p in method.GetParameters())
        {
            if (p.ParameterType == typeof(ChannelChatMessage)) continue;
            if (p.Name is not null && p.Name.Equals("bits", StringComparison.OrdinalIgnoreCase)) continue;

            var key = "{" + p.Name?.ToLowerInvariant() + "}";
            var desc = p.GetCustomAttribute<PlaceholderAttribute>()?.Description ?? $"Command argument '{p.Name}'";
            map[key] = desc;
        }

        var dataType = method.ReturnType.GetGenericArguments()[0].GetProperty("Data")!.PropertyType;

        foreach (var prop in dataType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var key = "{" + prop.Name.ToLowerInvariant() + "}";
            var desc = prop.GetCustomAttribute<PlaceholderAttribute>()?.Description ?? $"Result field '{prop.Name}'";
            map[key] = desc;
        }

        return map;
    }
}