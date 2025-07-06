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

using System.Globalization;
using System.Reflection;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;
using YouTubeMusicStreamer.Models;

namespace YouTubeMusicStreamer.Services.Commands.Binding;

public class ReflectionArgumentBinder : IArgumentBinder
{
    public async Task<BoundCommandResult> BindAndInvokeAsync(object handler, ChannelChatMessage message, IReadOnlyList<string> tokens, int bits)
    {
        var type = handler.GetType();
        var method = type.GetMethod("ExecuteCommandLogicAsync", BindingFlags.Instance | BindingFlags.NonPublic)
                     ?? throw new InvalidOperationException($"{type.Name} missing ExecuteCommandLogicAsync");

        var parameters = method.GetParameters();
        var args = new object?[parameters.Length];
        var queue = new Queue<string>(tokens);
        var argMap = new Dictionary<string, string>
        {
            ["{username}"] = message.ChatterUserName,
            ["{bits}"] = bits.ToString(),
        };

        for (var i = 0; i < parameters.Length; i++)
        {
            var p = parameters[i];
            if (p.ParameterType == typeof(ChannelChatMessage))
            {
                args[i] = message;
            }
            else if (p.Name is not null && p.Name.Equals("bits", StringComparison.OrdinalIgnoreCase) && p.ParameterType == typeof(int))
            {
                args[i] = bits;
            }
            else
            {
                var raw = queue.Count > 0
                    ? queue.Dequeue()
                    : p.HasDefaultValue
                        ? p.DefaultValue!.ToString()!
                        : throw new ArgumentException($"Missing '{p.Name}'");

                object? converted;
                try
                {
                    if (p.ParameterType == typeof(int))
                    {
                        if (!int.TryParse(raw, out var iv))
                            return new BoundCommandResult(success: false, data: null, argValues: new Dictionary<string, string> { { "{" + p.Name?.ToLower() + "}", raw } });
                        converted = iv;
                    }
                    else if (p.ParameterType == typeof(double))
                    {
                        if (!double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var dv))
                            return new BoundCommandResult(false, null, new Dictionary<string, string> { { "{" + p.Name?.ToLower() + "}", raw } });
                        converted = dv;
                    }
                    else
                    {
                        converted = Convert.ChangeType(raw, p.ParameterType, CultureInfo.InvariantCulture);
                    }
                }
                catch
                {
                    // any other conversion failure
                    return new BoundCommandResult(success: false, data: null, argValues: new Dictionary<string, string> { { "{" + p.Name?.ToLower() + "}", raw } });
                }

                args[i] = converted;
                argMap["{" + p.Name?.ToLowerInvariant() + "}"] = raw;
            }
        }

        var taskObj = method.Invoke(handler, args) ?? throw new InvalidOperationException("Invocation failed");
        var task = (Task)taskObj;
        await task.ConfigureAwait(false);

        var resultProp = task.GetType().GetProperty("Result") ?? throw new InvalidOperationException("No Result property");
        var rawResult = resultProp.GetValue(task)!;
        var cmdResType = rawResult.GetType();
        var success = (bool)cmdResType.GetProperty("Success")!.GetValue(rawResult)!;
        var data = cmdResType.GetProperty("Data")!.GetValue(rawResult);

        return new BoundCommandResult(success, data, argMap);
    }
}