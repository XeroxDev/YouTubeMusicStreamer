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

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using YouTubeMusicStreamer.Services.App.Diagnostics;

namespace YouTubeMusicStreamer.Services.App.Persistence;

[Table("AppDiagnostics")]
public sealed class AppDiagnosticEntity
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public AppDiagnosticSubsystem Subsystem { get; set; }

    [Required]
    public AppDiagnosticSeverity Severity { get; set; }

    [Required]
    public AppDiagnosticCategory Category { get; set; }

    [Required]
    public string Summary { get; set; } = string.Empty;

    public string? Detail { get; set; }

    public string? ExceptionText { get; set; }

    [Required]
    public DateTimeOffset CreatedAtUtc { get; set; }

    [Required]
    public AppDiagnosticVisibility Visibility { get; set; }
}
