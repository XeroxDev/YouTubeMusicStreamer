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

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Extensions.Logging;
using YouTubeMusicStreamer.Services.App;

namespace YouTubeMusicStreamer.Utils;

internal static class LoggingConfig
{
    private const LogLevel DefaultStartupLogLevel = LogLevel.Warning;
    private const string AppCategoryPrefix = "YouTubeMusicStreamer";
    private static readonly object SyncRoot = new();
    private static readonly LoggingLevelSwitch LevelSwitch = new();
    private static readonly AppPathProvider AppPathProvider = new();
    private static Serilog.ILogger? _logger;
    private static LogLevel _currentLogLevel = DefaultStartupLogLevel;
    private static bool _initialized;

    public static void ConfigureFileLogging(ILoggingBuilder lb)
    {
        lb.ClearProviders();
        Directory.CreateDirectory(AppPathProvider.LogDirectoryPath);
        lb.AddSerilog(GetOrCreateLogger(), dispose: false);
    }

    public static ILoggerFactory CreateLoggerFactory() => new SerilogLoggerFactory(GetOrCreateLogger(), dispose: false);

    public static void SetRuntimeLogLevel(LogLevel logLevel)
    {
        lock (SyncRoot)
        {
            _currentLogLevel = logLevel;
            LevelSwitch.MinimumLevel = ToSerilogLevel(logLevel);
        }
    }

    private static LogLevel GetStartupLogLevel()
    {
        try
        {
            if (File.Exists(AppPathProvider.DatabaseFilePath))
            {
                using var connection = new SqliteConnection($"Data Source={AppPathProvider.DatabaseFilePath}");
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT LogLevel FROM AppConfiguration LIMIT 1;";
                var value = command.ExecuteScalar();
                if (value is not null && Enum.TryParse<LogLevel>(value.ToString(), out var dbLogLevel))
                    return dbLogLevel;
            }

            return DefaultStartupLogLevel;
        }
        catch
        {
            return DefaultStartupLogLevel;
        }
    }

    private static Serilog.ILogger GetOrCreateLogger()
    {
        if (_initialized && _logger is not null)
            return _logger;

        lock (SyncRoot)
        {
            if (_initialized && _logger is not null)
                return _logger;

            _currentLogLevel = GetStartupLogLevel();
            LevelSwitch.MinimumLevel = ToSerilogLevel(_currentLogLevel);

            _logger = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(LevelSwitch)
                .Enrich.FromLogContext()
                .Filter.ByIncludingOnly(ShouldWriteEvent)
                .WriteTo.File(
                    Path.Combine(AppPathProvider.LogDirectoryPath, "log-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    outputTemplate: "{Timestamp:o} [{Level:u3}] {SourceContext}: {Message}{NewLine}{Exception}")
                .CreateLogger();

            _initialized = true;
            return _logger;
        }
    }

    private static bool ShouldWriteEvent(Serilog.Events.LogEvent logEvent)
    {
        return LoggingEventFilter.ShouldWriteEvent(logEvent, _currentLogLevel);
    }

    private static LogEventLevel ToSerilogLevel(LogLevel logLevel) => logLevel switch
    {
        LogLevel.Trace => LogEventLevel.Verbose,
        LogLevel.Debug => LogEventLevel.Debug,
        LogLevel.Information => LogEventLevel.Information,
        LogLevel.Warning => LogEventLevel.Warning,
        LogLevel.Error => LogEventLevel.Error,
        LogLevel.Critical => LogEventLevel.Fatal,
        LogLevel.None => (LogEventLevel)int.MaxValue,
        _ => LogEventLevel.Warning
    };

    internal static class LoggingEventFilter
    {
        public static bool ShouldWriteEvent(Serilog.Events.LogEvent logEvent, LogLevel currentLogLevel)
        {
            if (currentLogLevel != LogLevel.Information)
                return true;

            var sourceContext = TryGetSourceContext(logEvent);
            if (string.IsNullOrWhiteSpace(sourceContext))
                return true;

            if (IsAppCategory(sourceContext))
                return true;

            return logEvent.Level >= LogEventLevel.Warning;
        }

        private static bool IsAppCategory(string sourceContext) =>
            sourceContext.Equals("Program", StringComparison.Ordinal) ||
            sourceContext.StartsWith(AppCategoryPrefix, StringComparison.Ordinal);

        private static string? TryGetSourceContext(Serilog.Events.LogEvent logEvent)
        {
            if (!logEvent.Properties.TryGetValue("SourceContext", out var sourceContextValue))
                return null;

            return sourceContextValue is ScalarValue { Value: string sourceContext }
                ? sourceContext
                : null;
        }
    }
}
