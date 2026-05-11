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

using Microsoft.Extensions.Logging;

namespace YouTubeMusicStreamer.Services.App.Diagnostics;

public static class DiagnosticReportingExtensions
{
    public static DiagnosticReportBuilder<TCategoryName> Diagnostic<TCategoryName>(
        this ILogger<TCategoryName> logger,
        IAppDiagnosticsService diagnosticsService,
        AppDiagnosticSubsystem subsystem) =>
        new(logger, diagnosticsService, subsystem);
}

public sealed class DiagnosticReportBuilder<TCategoryName>
{
    private readonly ILogger<TCategoryName> _logger;
    private readonly IAppDiagnosticsService _diagnosticsService;
    private readonly AppDiagnosticSubsystem _subsystem;

    private LogLevel? _logLevel;
    private AppDiagnosticSeverity _severity = AppDiagnosticSeverity.Info;
    private AppDiagnosticCategory _category = AppDiagnosticCategory.InternalFault;
    private string? _summary;
    private string? _detail;
    private Exception? _exception;
    private AppDiagnosticVisibility _visibility = AppDiagnosticVisibility.DiagnosticsOnly;

    public DiagnosticReportBuilder(
        ILogger<TCategoryName> logger,
        IAppDiagnosticsService diagnosticsService,
        AppDiagnosticSubsystem subsystem)
    {
        _logger = logger;
        _diagnosticsService = diagnosticsService;
        _subsystem = subsystem;
    }

    public DiagnosticReportBuilder<TCategoryName> Info(AppDiagnosticCategory category, string summary)
    {
        _severity = AppDiagnosticSeverity.Info;
        _category = category;
        _summary = summary;
        _logLevel ??= LogLevel.Information;
        return this;
    }

    public DiagnosticReportBuilder<TCategoryName> Warning(AppDiagnosticCategory category, string summary)
    {
        _severity = AppDiagnosticSeverity.Warning;
        _category = category;
        _summary = summary;
        _logLevel ??= LogLevel.Warning;
        return this;
    }

    public DiagnosticReportBuilder<TCategoryName> Error(AppDiagnosticCategory category, string summary)
    {
        _severity = AppDiagnosticSeverity.Error;
        _category = category;
        _summary = summary;
        _logLevel ??= LogLevel.Error;
        return this;
    }

    public DiagnosticReportBuilder<TCategoryName> WithDetail(string? detail)
    {
        _detail = detail;
        return this;
    }

    public DiagnosticReportBuilder<TCategoryName> WithException(Exception? exception)
    {
        _exception = exception;
        return this;
    }

    public DiagnosticReportBuilder<TCategoryName> WithLogLevel(LogLevel logLevel)
    {
        _logLevel = logLevel;
        return this;
    }

    public DiagnosticReportBuilder<TCategoryName> Visibility(AppDiagnosticVisibility visibility)
    {
        _visibility = visibility;
        return this;
    }

    public DiagnosticReportBuilder<TCategoryName> Toast() => Visibility(AppDiagnosticVisibility.Toast);

    public DiagnosticReportBuilder<TCategoryName> StatusOnly() => Visibility(AppDiagnosticVisibility.StatusOnly);

    public DiagnosticReportBuilder<TCategoryName> DiagnosticsOnly() => Visibility(AppDiagnosticVisibility.DiagnosticsOnly);

    public AppDiagnostic Write()
    {
        if (string.IsNullOrWhiteSpace(_summary))
            throw new InvalidOperationException("A diagnostic summary must be provided before calling Write().");

        var logLevel = _logLevel ?? _severity switch
        {
            AppDiagnosticSeverity.Info => LogLevel.Information,
            AppDiagnosticSeverity.Warning => LogLevel.Warning,
            AppDiagnosticSeverity.Error => LogLevel.Error,
            _ => LogLevel.Error
        };

        if (string.IsNullOrWhiteSpace(_detail))
        {
            _logger.Log(logLevel, _exception, "{Summary}", _summary);
        }
        else
        {
            _logger.Log(logLevel, _exception, "{Summary}: {Detail}", _summary, _detail);
        }

        return _diagnosticsService.Record(
            _subsystem,
            _severity,
            _category,
            _summary,
            _detail,
            _exception,
            _visibility);
    }
}
