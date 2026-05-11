// This file is part of YouTubeMusicStreamer.
// Copyright (C) 2025 Dominic Ris
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

namespace YouTubeMusicStreamer.Services.Commands.Placeholders;

public class ReflectionPlaceholderProvider : IPlaceholderProvider
{
    private static readonly Dictionary<string, string> Global = new()
    {
        ["{username}"] = "The name of the user invoking the command",
        ["{bits}"] = "The total number of bits the user cheered"
    };

    public IReadOnlyDictionary<string, string> GetPlaceholders(Type commandType, MethodInfo method)
    {
        var map = new Dictionary<string, string>(Global);

        foreach (var parameter in method.GetParameters().Skip(1))
        {
            var key = "{" + parameter.Name?.ToLowerInvariant() + "}";
            var desc = parameter.GetCustomAttribute<PlaceholderAttribute>()?.Description ?? $"Command argument '{parameter.Name}'";
            map[key] = desc;
        }

        var dataType = Binding.CommandHandlerReflection.TryGetResultDataType(method);
        if (dataType is null || dataType == typeof(object))
            return map;

        foreach (var property in dataType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var key = "{" + property.Name.ToLowerInvariant() + "}";
            var desc = property.GetCustomAttribute<PlaceholderAttribute>()?.Description ?? $"Result field '{property.Name}'";
            map[key] = desc;
        }

        return map;
    }
}
