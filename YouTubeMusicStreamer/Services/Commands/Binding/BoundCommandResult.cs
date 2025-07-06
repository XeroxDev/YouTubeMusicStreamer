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

namespace YouTubeMusicStreamer.Services.Commands.Binding;

public class BoundCommandResult(bool success, object? data, IReadOnlyDictionary<string, string> argValues)
{
    /// <summary>True if the command logic succeeded.</summary>
    public bool Success { get; } = success;

    /// <summary>
    /// The strongly-typed DTO your command returned (or null).
    /// </summary>
    public object? Data { get; } = data;

    /// <summary>
    /// Maps each method-parameter (except ChatMessage and bits) to its stringifies value.
    /// Keys include the {braces}.
    /// </summary>
    public IReadOnlyDictionary<string, string> ArgValues { get; } = argValues;
}