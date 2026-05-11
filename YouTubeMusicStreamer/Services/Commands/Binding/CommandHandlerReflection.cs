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

using System.Reflection;
using YouTubeMusicStreamer.Attributes;

namespace YouTubeMusicStreamer.Services.Commands.Binding;

internal static class CommandHandlerReflection
{
    public static bool TryGetValidatedHandlerMethod(Type type, out MethodInfo? method, out string? error)
    {
        var methods = type
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Where(m => m.GetCustomAttribute<CommandExecutionAttribute>() is not null)
            .ToList();

        if (methods.Count == 0)
        {
            method = null;
            error = $"{type.Name} is missing a method marked with [CommandExecution].";
            return false;
        }

        if (methods.Count > 1)
        {
            method = null;
            error = $"{type.Name} has multiple methods marked with [CommandExecution].";
            return false;
        }

        var validatedMethod = methods[0];

        if (!validatedMethod.ReturnType.IsGenericType || validatedMethod.ReturnType.GetGenericTypeDefinition() != typeof(Task<>))
        {
            method = null;
            error = $"{type.Name}.{validatedMethod.Name} must return Task<CommandResult> or Task<CommandResult<TData>>.";
            return false;
        }

        var resultType = validatedMethod.ReturnType.GetGenericArguments()[0];
        if (!typeof(ICommandResult).IsAssignableFrom(resultType))
        {
            method = null;
            error = $"{type.Name}.{validatedMethod.Name} must return CommandResult or CommandResult<TData>.";
            return false;
        }

        var parameters = validatedMethod.GetParameters();
        if (parameters.Length == 0 || parameters[0].ParameterType != typeof(CommandContext))
        {
            method = null;
            error = $"{type.Name}.{validatedMethod.Name} must take CommandContext as its first parameter.";
            return false;
        }

        foreach (var parameter in parameters.Skip(1))
        {
            if (parameter.ParameterType == typeof(string) ||
                parameter.ParameterType == typeof(int) ||
                parameter.ParameterType == typeof(double))
            {
                continue;
            }

            method = null;
            error = $"{type.Name}.{validatedMethod.Name} uses unsupported parameter type '{parameter.ParameterType.Name}'.";
            return false;
        }

        method = validatedMethod;
        error = null;
        return true;
    }

    public static Type? TryGetResultDataType(MethodInfo method)
    {
        var taskResultType = method.ReturnType.GetGenericArguments()[0];
        if (!taskResultType.IsGenericType)
            return null;

        return taskResultType.GetProperty(nameof(CommandResult<object>.Value))?.PropertyType;
    }
}
