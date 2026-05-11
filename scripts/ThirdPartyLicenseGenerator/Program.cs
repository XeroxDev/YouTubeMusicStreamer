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

using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using System.Windows.Forms;

var options = GeneratorOptions.Parse(args);
var generator = new LicenseGenerator(options);
return await generator.RunAsync();

internal sealed class LicenseGenerator(GeneratorOptions options)
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    public async Task<int> RunAsync()
    {
        Directory.CreateDirectory(options.GeneratedRawFullPath);
        if (Directory.Exists(options.GeneratedLicensesFullPath))
        {
            Directory.Delete(options.GeneratedLicensesFullPath, true);
        }

        Directory.CreateDirectory(options.GeneratedLicensesFullPath);
        Directory.CreateDirectory(options.GeneratedPackageLicensesFullPath);

        var config = await LoadConfigAsync();
        var packages = LoadResolvedPackages();
        var results = new List<ThirdPartyLicenseOutput>();

        foreach (var package in packages)
        {
            var packageOverride = FindOverride(config.PackageOverrides, package.PackageId, package.PackageVersion);
            var result = await ResolvePackageAsync(package, packageOverride, config.NormalizationRules, config.ExpressionSources);
            results.Add(result);
        }

        results = results
            .OrderBy(result => result.PackageId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(result => result.PackageVersion, StringComparer.OrdinalIgnoreCase)
            .ToList();

        await File.WriteAllTextAsync(options.LicensesJsonPath, JsonSerializer.Serialize(results, _jsonOptions), Encoding.UTF8);

        WriteSummary(results);
        return 0;
    }

    private async Task<ThirdPartyLicenseOutput> ResolvePackageAsync(
        ResolvedPackage package,
        LicenseOverride? packageOverride,
        IReadOnlyList<LicenseNormalizationRule> normalizationRules,
        IReadOnlyDictionary<string, string> expressionSources)
    {
        var warnings = new List<ValidationErrorOutput>();
        var projectUrl = Coalesce(packageOverride?.ProjectUrl, package.ProjectUrl);
        var authors = Coalesce(packageOverride?.Authors, package.Authors);
        var copyright = Coalesce(packageOverride?.Copyright, package.Copyright);
        var onlineLicenseUrl = Coalesce(packageOverride?.LicenseUrl, package.LicenseUrl);
        var licenseDisplayName = Coalesce(packageOverride?.License, package.LicenseExpression, package.LicenseUrl);
        var normalizedExpression = NormalizeLicenseExpression(licenseDisplayName, onlineLicenseUrl, normalizationRules);
        var preferredPackageFileName = Coalesce(packageOverride?.PackageLicenseFile, package.LicenseFileName);
        var resolutionSource = "MetadataOnly";
        string? localLicensePath = null;

        var packageLicenseFile = FindPackageLicenseFile(package.PackageDirectory, preferredPackageFileName);
        if (packageLicenseFile is not null && IsSupportedLocalLicenseFile(packageLicenseFile.Extension))
        {
            var packageLicenseContent = await File.ReadAllTextAsync(packageLicenseFile.FullName, Encoding.UTF8);
            var inferredLocalLicenseLabel = InferLicenseDisplayName(packageLicenseContent, normalizationRules);
            localLicensePath = await CopyPackageLicenseAsync(package, packageLicenseFile);
            licenseDisplayName = normalizedExpression
                                 ?? inferredLocalLicenseLabel
                                 ?? packageOverride?.License
                                 ?? licenseDisplayName
                                 ?? "Local License";
            resolutionSource = "PackageFile";
        }
        else
        {
            var expressionSourceUrl = ResolveExpressionSourceUrl(normalizedExpression, expressionSources);
            if (!string.IsNullOrWhiteSpace(normalizedExpression) && !string.IsNullOrWhiteSpace(expressionSourceUrl))
            {
                var fetched = await TryFetchLicenseAsync(expressionSourceUrl);
                if (fetched is not null)
                {
                    localLicensePath = await WriteGeneratedExpressionLicenseAsync(
                        package,
                        normalizedExpression,
                        authors,
                        copyright,
                        projectUrl,
                        fetched.Value.Content);
                    onlineLicenseUrl ??= expressionSourceUrl;
                    licenseDisplayName = normalizedExpression;
                    resolutionSource = "Expression";
                }
                else
                {
                    warnings.Add(new ValidationErrorOutput(
                        "Failed to fetch authoritative license text for the normalized license expression.",
                        expressionSourceUrl));
                }
            }
            else if (packageOverride?.CanonicalLicenseExpression is { Length: > 0 } overrideExpression
                     && ResolveExpressionSourceUrl(overrideExpression, expressionSources) is { Length: > 0 } overrideExpressionSourceUrl)
            {
                var fetched = await TryFetchLicenseAsync(overrideExpressionSourceUrl);
                if (fetched is not null)
                {
                    localLicensePath = await WriteGeneratedExpressionLicenseAsync(
                        package,
                        overrideExpression,
                        authors,
                        copyright,
                        projectUrl,
                        fetched.Value.Content);
                    onlineLicenseUrl ??= overrideExpressionSourceUrl;
                    licenseDisplayName = overrideExpression;
                    resolutionSource = "Override";
                }
                else
                {
                    warnings.Add(new ValidationErrorOutput(
                        "Failed to fetch authoritative license text for the override-mapped license expression.",
                        overrideExpressionSourceUrl));
                }
            }
            else if (packageOverride?.InlineLicenseText is { Length: > 0 } inlineLicenseText)
            {
                localLicensePath = await WriteOverrideLicenseAsync(package, inlineLicenseText);
                licenseDisplayName ??= "Local License";
                resolutionSource = "Override";
            }
            else if (options.AllowNetworkFallback && !string.IsNullOrWhiteSpace(onlineLicenseUrl))
            {
                var fetched = await TryFetchLicenseAsync(onlineLicenseUrl);
                if (fetched is not null)
                {
                    localLicensePath = await WriteFetchedLicenseAsync(package, fetched.Value.Extension, fetched.Value.Content);
                    licenseDisplayName = InferLicenseDisplayName(fetched.Value.Content, normalizationRules)
                                         ?? packageOverride?.License
                                         ?? licenseDisplayName
                                         ?? "License Terms";
                    resolutionSource = "NetworkFallback";
                }
                else
                {
                    warnings.Add(new ValidationErrorOutput(
                        "Strict text-only network fallback rejected the fetched license content.",
                        onlineLicenseUrl));
                }
            }
        }

        if (package.ValidationWarnings.Count > 0)
        {
            warnings.AddRange(package.ValidationWarnings.Select(warning => new ValidationErrorOutput(warning, package.PackageDirectory.FullName)));
        }

        if (localLicensePath is null)
        {
            warnings.Add(new ValidationErrorOutput(
                "No trustworthy offline license artifact could be generated for this package.",
                package.PackageId));
        }

        if (string.IsNullOrWhiteSpace(licenseDisplayName))
        {
            licenseDisplayName = "License Terms";
        }
        else if (Uri.TryCreate(licenseDisplayName, UriKind.Absolute, out _))
        {
            licenseDisplayName = "License Terms";
        }

        return new ThirdPartyLicenseOutput(
            package.PackageId,
            package.PackageVersion,
            authors,
            licenseDisplayName,
            resolutionSource,
            localLicensePath,
            onlineLicenseUrl,
            projectUrl,
            copyright,
            warnings);
    }

    private async Task<LicenseGenerationConfig> LoadConfigAsync()
    {
        if (!File.Exists(options.OverridePath))
        {
            return new LicenseGenerationConfig([], [], new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        }

        var json = await File.ReadAllTextAsync(options.OverridePath, Encoding.UTF8);
        var config = JsonSerializer.Deserialize<LicenseGenerationConfig>(json, _jsonOptions);
        if (config is not null)
        {
            return new LicenseGenerationConfig(
                config.PackageOverrides ?? [],
                config.NormalizationRules ?? [],
                config.ExpressionSources ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        }

        var legacyOverrides = JsonSerializer.Deserialize<List<LicenseOverride>>(json, _jsonOptions) ?? [];
        return new LicenseGenerationConfig(legacyOverrides, [], new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
    }

    private List<ResolvedPackage> LoadResolvedPackages()
    {
        if (!File.Exists(options.ProjectAssetsPath))
        {
            throw new InvalidOperationException($"Missing resolved package graph: {options.ProjectAssetsPath}");
        }

        using var document = JsonDocument.Parse(File.ReadAllText(options.ProjectAssetsPath, Encoding.UTF8));
        var root = document.RootElement;
        var packageFolders = root.GetProperty("packageFolders").EnumerateObject().Select(property => property.Name).ToList();
        var packages = new Dictionary<string, ResolvedPackage>(StringComparer.OrdinalIgnoreCase);

        foreach (var target in root.GetProperty("targets").EnumerateObject())
        {
            foreach (var library in target.Value.EnumerateObject())
            {
                if (!library.Value.TryGetProperty("type", out var typeElement) || !string.Equals(typeElement.GetString(), "package", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var separator = library.Name.LastIndexOf('/');
                if (separator <= 0)
                {
                    continue;
                }

                var packageId = library.Name[..separator];
                var packageVersion = library.Name[(separator + 1)..];
                var key = $"{packageId}/{packageVersion}";
                if (packages.ContainsKey(key))
                {
                    continue;
                }

                var packageDirectory = ResolvePackageDirectory(packageFolders, packageId, packageVersion);
                if (packageDirectory is null)
                {
                    packages[key] = ResolvedPackage.Missing(packageId, packageVersion);
                    continue;
                }

                packages[key] = ReadResolvedPackage(packageId, packageVersion, packageDirectory);
            }
        }

        return packages.Values.ToList();
    }

    private static DirectoryInfo? ResolvePackageDirectory(IEnumerable<string> packageFolders, string packageId, string packageVersion)
    {
        foreach (var packageFolder in packageFolders)
        {
            var candidate = Path.Combine(packageFolder, packageId.ToLowerInvariant(), packageVersion.ToLowerInvariant());
            if (Directory.Exists(candidate))
            {
                return new DirectoryInfo(candidate);
            }
        }

        return null;
    }

    private static ResolvedPackage ReadResolvedPackage(string packageId, string packageVersion, DirectoryInfo packageDirectory)
    {
        var nuspecPath = Path.Combine(packageDirectory.FullName, $"{packageId.ToLowerInvariant()}.nuspec");
        if (!File.Exists(nuspecPath))
        {
            return ResolvedPackage.Missing(packageId, packageVersion, packageDirectory);
        }

        var document = XDocument.Load(nuspecPath);
        var metadata = document.Root?.Elements().FirstOrDefault(element => element.Name.LocalName == "metadata");
        if (metadata is null)
        {
            return ResolvedPackage.Missing(packageId, packageVersion, packageDirectory);
        }

        var licenseElement = metadata.Elements().FirstOrDefault(element => element.Name.LocalName == "license");
        var licenseType = licenseElement?.Attribute("type")?.Value;
        var licenseValue = licenseElement?.Value?.Trim();

        return new ResolvedPackage(
            packageId,
            packageVersion,
            packageDirectory,
            metadata.Elements().FirstOrDefault(element => element.Name.LocalName == "authors")?.Value?.Trim(),
            metadata.Elements().FirstOrDefault(element => element.Name.LocalName == "copyright")?.Value?.Trim(),
            metadata.Elements().FirstOrDefault(element => element.Name.LocalName == "projectUrl")?.Value?.Trim(),
            metadata.Elements().FirstOrDefault(element => element.Name.LocalName == "licenseUrl")?.Value?.Trim(),
            licenseType,
            licenseType == "expression" ? licenseValue : null,
            licenseType == "file" ? licenseValue : null,
            []);
    }

    private async Task<string> CopyPackageLicenseAsync(ResolvedPackage package, FileInfo sourceFile)
    {
        var extension = NormalizeExtension(sourceFile.Extension);
        var fileName = $"{SanitizeFileName(package.PackageId)}__{SanitizeFileName(package.PackageVersion)}{extension}";
        var fullPath = Path.Combine(options.GeneratedPackageLicensesFullPath, fileName);

        await using var sourceStream = sourceFile.OpenRead();
        await using var destinationStream = File.Create(fullPath);
        await sourceStream.CopyToAsync(destinationStream);

        return $"Generated/ThirdPartyLicenses/Packages/{fileName}";
    }

    private async Task<string> WriteGeneratedExpressionLicenseAsync(
        ResolvedPackage package,
        string expression,
        string? authors,
        string? copyright,
        string? projectUrl,
        string licenseText)
    {
        var fileName = $"{SanitizeFileName(package.PackageId)}__{SanitizeFileName(package.PackageVersion)}.txt";
        var fullPath = Path.Combine(options.GeneratedPackageLicensesFullPath, fileName);
        var builder = new StringBuilder();
        builder.AppendLine($"Package: {package.PackageId}");
        builder.AppendLine($"Version: {package.PackageVersion}");
        builder.AppendLine($"License: {expression}");

        if (!string.IsNullOrWhiteSpace(authors))
        {
            builder.AppendLine($"Authors: {authors}");
        }

        if (!string.IsNullOrWhiteSpace(copyright))
        {
            builder.AppendLine($"Copyright: {copyright}");
        }

        if (!string.IsNullOrWhiteSpace(projectUrl))
        {
            builder.AppendLine($"Project URL: {projectUrl}");
        }

        builder.AppendLine();
        builder.AppendLine($"{expression} license text:");
        builder.AppendLine();
        builder.Append(licenseText.Trim());

        await File.WriteAllTextAsync(fullPath, builder.ToString(), Encoding.UTF8);
        return $"Generated/ThirdPartyLicenses/Packages/{fileName}";
    }

    private async Task<string> WriteOverrideLicenseAsync(ResolvedPackage package, string content)
    {
        var fileName = $"{SanitizeFileName(package.PackageId)}__{SanitizeFileName(package.PackageVersion)}.txt";
        var fullPath = Path.Combine(options.GeneratedPackageLicensesFullPath, fileName);
        await File.WriteAllTextAsync(fullPath, content, Encoding.UTF8);
        return $"Generated/ThirdPartyLicenses/Packages/{fileName}";
    }

    private async Task<string> WriteFetchedLicenseAsync(ResolvedPackage package, string extension, string content)
    {
        if (string.Equals(extension, ".rtf", StringComparison.OrdinalIgnoreCase))
        {
            content = ConvertRtfToPlainText(content);
            extension = ".txt";
        }

        var fileName = $"{SanitizeFileName(package.PackageId)}__{SanitizeFileName(package.PackageVersion)}{extension}";
        var fullPath = Path.Combine(options.GeneratedPackageLicensesFullPath, fileName);
        await File.WriteAllTextAsync(fullPath, content, Encoding.UTF8);
        return $"Generated/ThirdPartyLicenses/Packages/{fileName}";
    }

    private async Task<FetchedLicense?> TryFetchLicenseAsync(string licenseUrl)
    {
        try
        {
            using var response = await _httpClient.GetAsync(licenseUrl);
            if (response.StatusCode is HttpStatusCode.MultipleChoices or HttpStatusCode.Moved or HttpStatusCode.Redirect or HttpStatusCode.RedirectMethod)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();

            var mediaType = response.Content.Headers.ContentType?.MediaType?.ToLowerInvariant();
            if (mediaType is not null && mediaType.Contains("html", StringComparison.Ordinal))
            {
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            if (LooksLikeHtmlPage(content))
            {
                return null;
            }

            if (!LooksLikeTextDocument(content))
            {
                return null;
            }

            var extension = DetectFetchedLicenseExtension(mediaType, content);

            return new FetchedLicense(extension, content);
        }
        catch
        {
            return null;
        }
    }

    private static string DetectFetchedLicenseExtension(string? mediaType, string content)
    {
        if (LooksLikeRtfDocument(content))
        {
            return ".rtf";
        }

        return mediaType switch
        {
            "text/markdown" => ".md",
            "application/rtf" => ".rtf",
            _ => ".txt"
        };
    }

    private static bool LooksLikeRtfDocument(string content)
        => content.TrimStart().StartsWith(@"{\rtf1", StringComparison.OrdinalIgnoreCase);

    private static string ConvertRtfToPlainText(string rtf)
    {
        var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        var thread = new Thread(() =>
        {
            try
            {
                using var richTextBox = new RichTextBox
                {
                    Rtf = rtf
                };

                completion.SetResult(richTextBox.Text);
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        return completion.Task.GetAwaiter().GetResult();
    }

    private static FileInfo? FindPackageLicenseFile(DirectoryInfo packageDirectory, string? preferredPackageFileName)
    {
        if (!string.IsNullOrWhiteSpace(preferredPackageFileName))
        {
            var explicitFile = new FileInfo(Path.Combine(packageDirectory.FullName, preferredPackageFileName));
            if (explicitFile.Exists)
            {
                return explicitFile;
            }
        }

        var acceptedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "license",
            "copying",
            "notice",
            "additional-permissions"
        };

        var acceptedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            string.Empty,
            ".txt",
            ".md",
            ".rtf"
        };

        foreach (var filePath in Directory.EnumerateFiles(packageDirectory.FullName))
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var extension = Path.GetExtension(filePath);
            if (!acceptedNames.Contains(fileName) || !acceptedExtensions.Contains(extension))
            {
                continue;
            }

            return new FileInfo(filePath);
        }

        return null;
    }

    private static bool IsSupportedLocalLicenseFile(string extension)
        => NormalizeExtension(extension) is ".txt" or ".md" or ".rtf";

    private static string NormalizeExtension(string extension)
        => string.IsNullOrWhiteSpace(extension)
            ? ".txt"
            : extension.StartsWith('.') ? extension.ToLowerInvariant() : $".{extension.ToLowerInvariant()}";

    private static string? NormalizeLicenseExpression(string? displayName, string? onlineLicenseUrl, IReadOnlyList<LicenseNormalizationRule> normalizationRules)
    {
        foreach (var rule in normalizationRules)
        {
            var matchesDisplayName = !string.IsNullOrWhiteSpace(rule.MatchLicense)
                                     && string.Equals(rule.MatchLicense, displayName, StringComparison.OrdinalIgnoreCase);
            var matchesLicenseUrl = !string.IsNullOrWhiteSpace(rule.MatchLicenseUrl)
                                    && string.Equals(rule.MatchLicenseUrl, onlineLicenseUrl, StringComparison.OrdinalIgnoreCase);
            if (matchesDisplayName || matchesLicenseUrl)
            {
                return rule.CanonicalLicenseExpression;
            }
        }

        return null;
    }

    private static string? ResolveExpressionSourceUrl(string? expression, IReadOnlyDictionary<string, string> expressionSources)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return null;
        }

        if (expressionSources.TryGetValue(expression, out var configuredSourceUrl))
        {
            return configuredSourceUrl;
        }

        return IsSimpleSpdxIdentifier(expression)
            ? $"https://spdx.org/licenses/{expression}.txt"
            : null;
    }

    private static bool IsSimpleSpdxIdentifier(string expression)
        => expression.All(character => char.IsLetterOrDigit(character) || character is '.' or '-');

    private static string? InferLicenseDisplayName(string content, IReadOnlyList<LicenseNormalizationRule> normalizationRules)
    {
        foreach (var rule in normalizationRules)
        {
            if (string.IsNullOrWhiteSpace(rule.MatchContentContains))
            {
                continue;
            }

            if (content.Contains(rule.MatchContentContains, StringComparison.OrdinalIgnoreCase))
            {
                return rule.DisplayName ?? rule.CanonicalLicenseExpression;
            }
        }

        return null;
    }

    private static bool LooksLikeHtmlPage(string content)
    {
        var trimmed = content.TrimStart();
        return trimmed.StartsWith("<!DOCTYPE html>", StringComparison.OrdinalIgnoreCase)
               || trimmed.StartsWith("<html", StringComparison.OrdinalIgnoreCase)
               || trimmed.Contains("<head", StringComparison.OrdinalIgnoreCase)
               || trimmed.Contains("github.githubassets.com", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeTextDocument(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        if (content.TrimStart().StartsWith("{\\rtf", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var nonPrintable = content.Count(character => char.IsControl(character) && character is not '\r' and not '\n' and not '\t');
        return nonPrintable < Math.Max(4, content.Length / 200);
    }

    private static string SanitizeFileName(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(char.IsLetterOrDigit(character) ? character : '_');
        }

        return builder.ToString().Trim('_');
    }

    private static string? Coalesce(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static LicenseOverride? FindOverride(IEnumerable<LicenseOverride> overrides, string packageId, string packageVersion)
        => overrides.FirstOrDefault(licenseOverride =>
            string.Equals(licenseOverride.Id, packageId, StringComparison.OrdinalIgnoreCase)
            && (string.IsNullOrWhiteSpace(licenseOverride.Version)
                || string.Equals(licenseOverride.Version, packageVersion, StringComparison.OrdinalIgnoreCase)));

    private static void WriteSummary(IEnumerable<ThirdPartyLicenseOutput> results)
    {
        var grouped = results
            .GroupBy(result => result.LicenseSource)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase);

        Console.WriteLine("Third-party license generation summary");
        foreach (var group in grouped)
        {
            Console.WriteLine($"- {group.Key}: {group.Count()}");
        }

        var unresolved = results.Where(result => result.LocalLicensePath is null).ToList();
        if (unresolved.Count == 0)
        {
            Console.WriteLine("- unresolved: 0");
            return;
        }

        Console.WriteLine($"- unresolved: {unresolved.Count}");
        foreach (var package in unresolved)
        {
            Console.WriteLine($"  * {package.PackageId} {package.PackageVersion}");
        }
    }
}

internal readonly record struct GeneratorOptions(
    string ProjectPath,
    string GeneratedRawFullPath,
    string GeneratedLicensesFullPath,
    string GeneratedPackageLicensesFullPath,
    string LicensesJsonPath,
    string ProjectAssetsPath,
    string OverridePath,
    bool AllowNetworkFallback)
{
    public static GeneratorOptions Parse(string[] args)
    {
        var arguments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < args.Length - 1; index += 2)
        {
            arguments[args[index]] = args[index + 1];
        }

        var projectPath = Path.GetFullPath(arguments.GetValueOrDefault("--project") ?? "YouTubeMusicStreamer/YouTubeMusicStreamer.csproj");
        var generatedRawDir = Path.GetFullPath(arguments.GetValueOrDefault("--generatedRawDir") ?? "YouTubeMusicStreamer/Resources/Raw/Generated");
        var projectDirectory = Path.GetDirectoryName(projectPath) ?? throw new InvalidOperationException("Project path must have a directory.");
        var overridePath = Path.GetFullPath(arguments.GetValueOrDefault("--override") ?? Path.Combine(projectDirectory, "license-override.json"));

        return new GeneratorOptions(
            projectPath,
            generatedRawDir,
            Path.Combine(generatedRawDir, "ThirdPartyLicenses"),
            Path.Combine(generatedRawDir, "ThirdPartyLicenses", "Packages"),
            Path.Combine(generatedRawDir, "third-party-licenses.json"),
            Path.Combine(projectDirectory, "obj", "project.assets.json"),
            overridePath,
            !string.Equals(arguments.GetValueOrDefault("--networkFallback"), "false", StringComparison.OrdinalIgnoreCase));
    }
}

internal sealed record LicenseGenerationConfig(
    [property: JsonPropertyName("PackageOverrides")] IReadOnlyList<LicenseOverride> PackageOverrides,
    [property: JsonPropertyName("NormalizationRules")] IReadOnlyList<LicenseNormalizationRule> NormalizationRules,
    [property: JsonPropertyName("ExpressionSources")] IReadOnlyDictionary<string, string> ExpressionSources
);

internal sealed record LicenseOverride(
    [property: JsonPropertyName("Id")] string Id,
    [property: JsonPropertyName("Version")] string? Version,
    [property: JsonPropertyName("License")] string? License,
    [property: JsonPropertyName("LicenseUrl")] string? LicenseUrl,
    [property: JsonPropertyName("Authors")] string? Authors,
    [property: JsonPropertyName("Copyright")] string? Copyright,
    [property: JsonPropertyName("ProjectUrl")] string? ProjectUrl,
    [property: JsonPropertyName("PackageLicenseFile")] string? PackageLicenseFile,
    [property: JsonPropertyName("CanonicalLicenseExpression")] string? CanonicalLicenseExpression,
    [property: JsonPropertyName("InlineLicenseText")] string? InlineLicenseText
);

internal sealed record LicenseNormalizationRule(
    [property: JsonPropertyName("MatchLicense")] string? MatchLicense,
    [property: JsonPropertyName("MatchLicenseUrl")] string? MatchLicenseUrl,
    [property: JsonPropertyName("MatchContentContains")] string? MatchContentContains,
    [property: JsonPropertyName("CanonicalLicenseExpression")] string CanonicalLicenseExpression,
    [property: JsonPropertyName("DisplayName")] string? DisplayName
);

internal sealed record ResolvedPackage(
    string PackageId,
    string PackageVersion,
    DirectoryInfo PackageDirectory,
    string? Authors,
    string? Copyright,
    string? ProjectUrl,
    string? LicenseUrl,
    string? LicenseType,
    string? LicenseExpression,
    string? LicenseFileName,
    IReadOnlyList<string> ValidationWarnings)
{
    public static ResolvedPackage Missing(string packageId, string packageVersion, DirectoryInfo? packageDirectory = null)
        => new(
            packageId,
            packageVersion,
            packageDirectory ?? new DirectoryInfo(Path.Combine(Path.GetTempPath(), "missing-package")),
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            [$"Package metadata could not be read from the local NuGet cache for {packageId} {packageVersion}."]);
}

internal sealed record ThirdPartyLicenseOutput(
    [property: JsonPropertyName("PackageId")] string PackageId,
    [property: JsonPropertyName("PackageVersion")] string PackageVersion,
    [property: JsonPropertyName("Authors")] string? Authors,
    [property: JsonPropertyName("LicenseDisplayName")] string LicenseDisplayName,
    [property: JsonPropertyName("LicenseSource")] string LicenseSource,
    [property: JsonPropertyName("LocalLicensePath")] string? LocalLicensePath,
    [property: JsonPropertyName("OnlineLicenseUrl")] string? OnlineLicenseUrl,
    [property: JsonPropertyName("PackageProjectUrl")] string? PackageProjectUrl,
    [property: JsonPropertyName("Copyright")] string? Copyright,
    [property: JsonPropertyName("ValidationErrors")] IReadOnlyList<ValidationErrorOutput> ValidationErrors
);

internal sealed record ValidationErrorOutput(
    [property: JsonPropertyName("Error")] string Error,
    [property: JsonPropertyName("Context")] string Context
);

internal readonly record struct FetchedLicense(string Extension, string Content);
