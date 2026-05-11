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

using System.Globalization;
using System.Reflection;
using YouTubeMusicStreamer.Attributes;

namespace YouTubeMusicStreamer.Services.Commands.Binding;

public class ReflectionArgumentBinder : IArgumentBinder
{
    public async Task<BoundCommandResult> BindAndInvokeAsync(object handler, MethodInfo method, CommandContext context, IReadOnlyList<string> tokens)
    {
        var parameters = method.GetParameters();
        var args = new object?[parameters.Length];
        args[0] = context;

        var queue = new Queue<string>(tokens);
        var argMap = new Dictionary<string, string>
        {
            ["{username}"] = context.ExecutorDisplayName,
            ["{bits}"] = context.Bits.ToString(CultureInfo.InvariantCulture)
        };

        for (var i = 1; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            if (!TryTakeRawValue(queue, parameter, out var rawValue, out var missingResult))
                return new BoundCommandResult(missingResult!, argMap);

            if (!TryConvertArgument(parameter, rawValue!, out var converted, out var invalidResult))
                return new BoundCommandResult(invalidResult!, AddArgValue(argMap, parameter, rawValue!));

            args[i] = converted;
            AddArgValue(argMap, parameter, rawValue!);
        }

        var taskObject = method.Invoke(handler, args) ?? throw new InvalidOperationException("Command invocation returned null.");
        var task = (Task)taskObject;
        await task.ConfigureAwait(false);

        var rawResult = task.GetType().GetProperty("Result")?.GetValue(task) as ICommandResult
                        ?? throw new InvalidOperationException("Command invocation did not produce a command result.");

        return new BoundCommandResult(rawResult, argMap);
    }

    private static bool TryTakeRawValue(
        Queue<string> queue,
        ParameterInfo parameter,
        out string? rawValue,
        out ICommandResult? result)
    {
        var argumentAttribute = parameter.GetCustomAttribute<CommandArgumentAttribute>();
        if (argumentAttribute?.RestOfInput == true)
        {
            rawValue = queue.Count > 0 ? string.Join(' ', queue) : null;
            queue.Clear();

            if (!string.IsNullOrWhiteSpace(rawValue))
            {
                result = null;
                return true;
            }

            if (parameter.HasDefaultValue)
            {
                rawValue = parameter.DefaultValue?.ToString() ?? string.Empty;
                result = null;
                return true;
            }

            result = CommandResult.BadInput(
                CommandExecutionReason.MissingArgument,
                $"Missing '{parameter.Name}'.");
            return false;
        }

        if (queue.Count > 0)
        {
            rawValue = queue.Dequeue();
            result = null;
            return true;
        }

        if (parameter.HasDefaultValue)
        {
            rawValue = parameter.DefaultValue?.ToString() ?? string.Empty;
            result = null;
            return true;
        }

        rawValue = null;
        result = CommandResult.BadInput(
            CommandExecutionReason.MissingArgument,
            $"Missing '{parameter.Name}'.");
        return false;
    }

    private static bool TryConvertArgument(
        ParameterInfo parameter,
        string rawValue,
        out object? converted,
        out ICommandResult? result)
    {
        try
        {
            if (parameter.ParameterType == typeof(int))
            {
                if (!int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
                {
                    converted = null;
                    result = CommandResult.BadInput(
                        CommandExecutionReason.InvalidArgument,
                        $"'{rawValue}' is not a valid value for '{parameter.Name}'.");
                    return false;
                }

                converted = intValue;
                result = null;
                return true;
            }

            if (parameter.ParameterType == typeof(double))
            {
                if (!double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleValue))
                {
                    converted = null;
                    result = CommandResult.BadInput(
                        CommandExecutionReason.InvalidArgument,
                        $"'{rawValue}' is not a valid value for '{parameter.Name}'.");
                    return false;
                }

                converted = doubleValue;
                result = null;
                return true;
            }

            if (parameter.ParameterType == typeof(string))
            {
                converted = rawValue;
                result = null;
                return true;
            }

            converted = Convert.ChangeType(rawValue, parameter.ParameterType, CultureInfo.InvariantCulture);
            result = null;
            return true;
        }
        catch
        {
            converted = null;
            result = CommandResult.BadInput(
                CommandExecutionReason.InvalidArgument,
                $"'{rawValue}' is not a valid value for '{parameter.Name}'.");
            return false;
        }
    }

    private static Dictionary<string, string> AddArgValue(
        IDictionary<string, string> map,
        ParameterInfo parameter,
        string rawValue)
    {
        if (!string.IsNullOrWhiteSpace(parameter.Name))
            map["{" + parameter.Name.ToLowerInvariant() + "}"] = rawValue;

        return new Dictionary<string, string>(map);
    }
}
