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

using System.Text.Json.Serialization;

namespace YouTubeMusicStreamer.Models;

[JsonSerializable(typeof(ThirdPartyLicense))]
public record ThirdPartyLicense(
    [property: JsonPropertyName("PackageId")] string PackageId,
    [property: JsonPropertyName("PackageVersion")] string PackageVersion,
    [property: JsonPropertyName("Authors")] string? Authors,
    [property: JsonPropertyName("License")] string? License,
    [property: JsonPropertyName("LicenseUrl")] string LicenseUrl,
    [property: JsonPropertyName("LicenseInformationOrigin")] int LicenseInformationOrigin,
    [property: JsonPropertyName("PackageProjectUrl")] string? PackageProjectUrl,
    [property: JsonPropertyName("Copyright")] string? Copyright,
    [property: JsonPropertyName("ValidationErrors")] IReadOnlyList<ValidationError> ValidationErrors
);

[JsonSerializable(typeof(ValidationError))]
public record ValidationError(
    [property: JsonPropertyName("Error")] string Error,
    [property: JsonPropertyName("Context")] string Context
);