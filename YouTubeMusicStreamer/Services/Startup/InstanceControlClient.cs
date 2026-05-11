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

using System.IO.Pipes;
using System.Text.Json;
using YouTubeMusicStreamer.Services.App;

namespace YouTubeMusicStreamer.Services.Startup;

internal static class InstanceControlClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<InstanceCommandResult?> TryShowWindowAsync(int attempts = 3, int timeoutMs = 500)
    {
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            var result = await TrySendAsync(new InstanceCommand(InstanceCommandType.ShowWindow), timeoutMs);
            if (result?.Success == true)
                return result;

            await Task.Delay(150);
        }

        return null;
    }

    public static async Task<InstanceCommandResult?> TrySendAsync(InstanceCommand command, int timeoutMs)
    {
        try
        {
            await using var stream = new NamedPipeClientStream(
                ".",
                GetPipeName(),
                PipeDirection.InOut,
                PipeOptions.Asynchronous);

            using var cts = new CancellationTokenSource(timeoutMs);
            await stream.ConnectAsync(cts.Token);

            using var writer = new StreamWriter(stream) { AutoFlush = true };
            using var reader = new StreamReader(stream);

            await writer.WriteLineAsync(JsonSerializer.Serialize(command, JsonOptions));
            var responseJson = await reader.ReadLineAsync(cts.Token);

            return string.IsNullOrWhiteSpace(responseJson)
                ? null
                : JsonSerializer.Deserialize<InstanceCommandResult>(responseJson, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    internal static string GetPipeName() => $"{new AssemblyAppIdentitySource().AppName}.startup";
}
