using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using Windows.Data.Html;
using Blazored.Toast.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using YouTubeMusicStreamer.Services;
using YouTubeMusicStreamer.Services.App;
using YouTubeMusicStreamer.Utils;

namespace YouTubeMusicStreamer.Components.Pages.Logging;

public partial class Logging(SettingsService settingsService, IToastService toastService, ILogger<Logging> logger) : ComponentBase, IDisposable
{
    private LogLevel _logLevel;
    private List<string> _logLines = [];
    private FileSystemWatcher? _fileSystemWatcher;
    private bool _colorlessMode = false;

    protected override void OnInitialized()
    {
        ResetGlobalSettings(false);
        _ = Task.Run(() =>
        {
            var logFile = Path.Combine(AppUtils.LogFolder, AppUtils.CurrentLogFile);
            SetLogLines(GetLogLines(logFile));

            _fileSystemWatcher = new FileSystemWatcher
            {
                Path = AppUtils.LogFolder,
                Filter = AppUtils.CurrentLogFile,
                NotifyFilter = NotifyFilters.LastWrite,
                EnableRaisingEvents = true
            };

            _fileSystemWatcher.Changed += OnFileSystemWatcherOnChanged;
        });
    }

    private void OnFileSystemWatcherOnChanged(object sender, FileSystemEventArgs args) => SetLogLines(GetLogLines(args.FullPath));

    private void SetLogLines(List<string>? lines)
    {
        _logLines = lines?.TakeLast(100).ToList() ?? [];
        InvokeAsync(StateHasChanged);
    }

    private async Task SaveGlobalSettings()
    {
        await settingsService.SaveAppSettingAsync(s => { s.LogLevel = _logLevel; });

        toastService.ShowSuccess("Settings saved successfully.");
    }

    private void ResetGlobalSettings(bool notify = true)
    {
        _logLevel = settingsService.GetAppSettings().LogLevel;

        if (notify)
        {
            toastService.ShowSuccess("Settings reset to previous saved state.");
        }
    }

    private static List<string> GetLogLevels() => Enum.GetValues<LogLevel>().Select(l => l.ToString()).ToList();

    private static void OpenLogFolder()
    {
        var logPath = Path.Combine(AppUtils.FilePath, "Logs");
        Process.Start(new ProcessStartInfo("explorer.exe", logPath));
    }

    private List<string> GetLogLines(string fullPath)
    {
        if (!File.Exists(fullPath))
        {
            return [];
        }

        var lines = new List<string>();
        try
        {
            using var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var streamReader = new StreamReader(fileStream);

            while (streamReader.ReadLine() is { } line)
            {
                lines.Add(line);
            }

            streamReader.Close();
            fileStream.Close();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to read log file");
        }

        return lines;
    }

    public static string PrettyLine(string line, bool colorlessMode)
    {
        line = line.Replace("&", "&amp;", StringComparison.OrdinalIgnoreCase);
        line = line.Replace("<", "&lt;", StringComparison.OrdinalIgnoreCase);
        line = line.Replace(">", "&gt;", StringComparison.OrdinalIgnoreCase);
        // Remove the app name prefix if it exists.
        line = line.Replace($"{AppUtils.AppName}.", "", StringComparison.OrdinalIgnoreCase);

        if (colorlessMode)
        {
            return line;
        }

        var match = LogRegex().Match(line);
        if (!match.Success)
        {
            // If the line doesn't match the overall log structure,
            // process the entire line as a message.
            return ProcessMessage(line);
        }

        // Process the ISO timestamp.
        var isoHtml =
            $"<span class='log-date'>{match.Groups["date"].Value}</span>" +
            $"<span class='log-time-extra'>{match.Groups["T"].Value}</span>" +
            $"<span class='log-date'>{match.Groups["time"].Value}</span>" +
            $"<span class='log-time-extra'>{match.Groups["frac"].Value}</span>";

        // Process the log level dynamically.
        var levelText = match.Groups["level"].Value;
        var level = levelText.Trim('[', ']');
        var levelHtml = $"<span class='log-level-{level.ToLowerInvariant()}'>[{level}]</span>";

        // Process the scope.
        var scopeHtml = $"<span class='log-scope'>{match.Groups["scope"].Value}</span>{match.Groups["colon"].Value}";

        // Process the message part with additional highlighting.
        var messageHtml = ProcessMessage(match.Groups["message"].Value);

        // Combine all parts.
        return $"{isoHtml} {levelHtml} {scopeHtml} {messageHtml}";
    }

    private static string ProcessMessage(string message)
    {
        message = BoolRegex().Replace(message, m =>
        {
            var lower = m.Value.ToLowerInvariant();
            return lower == "true"
                ? $"<span class='log-true'>{m.Value}</span>"
                : $"<span class='log-false'>{m.Value}</span>";
        });

        message = IsoRegex().Replace(message, m =>
        {
            var date = m.Groups["date"].Value;
            var time = m.Groups["time"].Value;
            var frac = m.Groups["frac"].Value;
            var z = m.Groups["Z"].Value;

            return $"<span class='log-date'>{date}</span>" +
                   $"<span class='log-time-extra'>{m.Groups["T"].Value}</span>" +
                   $"<span class='log-time'>{time}</span>" +
                   $"<span class='log-time-extra'>{frac}</span>" +
                   $"<span class='log-time-extra'>{z}</span>";
        });

        // Highlight numbers (both integers and floats)
        message = NumberRegex().Replace(message, m =>
            $"<span class='log-number'>{m.Value}</span>");

        // Highlight quoted text
        message = QuoteRegex().Replace(message, m =>
            $"<span class='log-quote'>\"{m.Groups[1].Value}\"</span>");
        
        if (message.StartsWith(' ') || message.StartsWith('\t'))
        {
            message = $"<span class='log-quote'>{message}</span>";
        }

        // Wrap the processed message in default text styling.
        return $"<span class='log-text'>{message}</span>";
    }


    public void Dispose()
    {
        _fileSystemWatcher?.Dispose();
        GC.SuppressFinalize(this);
    }

    #region Regex

    [GeneratedRegex(@"\b(true|false)\b", RegexOptions.IgnoreCase)]
    private static partial Regex BoolRegex();

    [GeneratedRegex(@"\d")]
    private static partial Regex NumberRegex();

    [GeneratedRegex("\"([^\"]*)\"")]
    private static partial Regex QuoteRegex();

    [GeneratedRegex(@"^(?<iso>(?<date>\d{4}-\d{2}-\d{2})(?<T>T)(?<time>\d{2}:\d{2}:\d{2})(?<frac>\.\d+(?:[+-]\d{2}:\d{2})?))\s+(?<level>\[[A-Z]+\])\s+(?<scope>[^:]+)(?<colon>:)\s+(?<message>.*)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex LogRegex();

    [GeneratedRegex(@"(?<iso>(?<date>\d{4}-\d{2}-\d{2})(?<T>T)(?<time>\d{2}:\d{2}:\d{2})(?<frac>\.\d+(?:[+-]\d{2}:\d{2})?)(?<Z>Z?))")]
    private static partial Regex IsoRegex();

    #endregion
}