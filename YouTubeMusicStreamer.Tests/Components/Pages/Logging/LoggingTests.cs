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

using LoggingPageComponent = YouTubeMusicStreamer.Components.Pages.Logging.Logging;
using YouTubeMusicStreamer.Services.App;

namespace YouTubeMusicStreamer.Tests.Components.Pages.LoggingPage;

public class LoggingTests
{
    [Fact]
    public void PrettyLine_ReturnsEscapedRawText_WhenColorlessModeIsEnabled()
    {
        var line = $"2026-04-04T21:19:39.4787550+02:00 [INF] {AssemblyAppIdentitySource.DefaultAppName}.Scope: <ready> & \"quoted\"";

        var result = LoggingPageComponent.PrettyLine(line, colorlessMode: true);

        Assert.DoesNotContain($"{AssemblyAppIdentitySource.DefaultAppName}.", result);
        Assert.Contains("&lt;ready&gt;", result);
        Assert.Contains("&amp;", result);
        Assert.Contains("\"quoted\"", result);
        Assert.DoesNotContain("<span", result);
    }

    [Fact]
    public void PrettyLine_FormatsStructuredLogLineAndRemovesAppPrefix_WhenColorlessModeIsDisabled()
    {
        var line = $"2026-04-04T21:19:39.4787550+02:00 [INF] {AssemblyAppIdentitySource.DefaultAppName}.Services.Startup.StartupCoordinator: Connected \"music\" with 42 retries left false";

        var result = LoggingPageComponent.PrettyLine(line, colorlessMode: false);

        Assert.Contains("<span class='log-date'>2026-04-04</span>", result);
        Assert.Contains("<span class='log-level-inf'>[INF]</span>", result);
        Assert.Contains("<span class='log-scope'>Services.Startup.StartupCoordinator</span>:", result);
        Assert.Contains("<span class='log-quote'>\"music\"</span>", result);
        Assert.Contains("<span class='log-number'>4</span><span class='log-number'>2</span>", result);
        Assert.Contains("<span class='log-false'>false</span>", result);
        Assert.DoesNotContain($"{AssemblyAppIdentitySource.DefaultAppName}.", result);
    }

    [Fact]
    public void PrettyLine_ProcessesUnstructuredMessages_WhenRegexDoesNotMatch()
    {
        const string line = "  indented true 2026-04-04T21:19:39.4787550Z";

        var result = LoggingPageComponent.PrettyLine(line, colorlessMode: false);

        Assert.StartsWith("<span class='log-text'><span class='log-quote'>", result, StringComparison.Ordinal);
        Assert.Contains("<span class='log-true'>true</span>", result);
        Assert.Contains("class='log-date'", result);
        Assert.Contains("class='log-time'", result);
        Assert.Contains("class='log-number'>2</span>", result);
    }

    [Fact]
    public void PrettyLine_EscapesHtmlBeforeApplyingColorFormatting_WhenColorlessModeIsDisabled()
    {
        var line = $"2026-04-04T21:19:39.4787550+02:00 [WRN] {AssemblyAppIdentitySource.DefaultAppName}.Scope: <unsafe> & \"quoted\"";

        var result = LoggingPageComponent.PrettyLine(line, colorlessMode: false);

        Assert.Contains("&lt;unsafe&gt;", result);
        Assert.Contains("&amp;", result);
        Assert.Contains("<span class='log-quote'>\"quoted\"</span>", result);
        Assert.DoesNotContain("<unsafe>", result);
    }

    [Fact]
    public void PrettyLine_DoesNotHighlightBooleanSubstrings_WhenWordsOnlyContainTrueOrFalse()
    {
        const string line = "truthy falsehood true false";

        var result = LoggingPageComponent.PrettyLine(line, colorlessMode: false);

        Assert.Contains("truthy", result);
        Assert.Contains("falsehood", result);
        Assert.Contains("<span class='log-true'>true</span>", result);
        Assert.Contains("<span class='log-false'>false</span>", result);
        Assert.DoesNotContain("<span class='log-true'>truthy</span>", result);
        Assert.DoesNotContain("<span class='log-false'>falsehood</span>", result);
    }

    [Fact]
    public void PrettyLine_RemovesAppPrefixCaseInsensitively_WhenLineIsUnstructured()
    {
        var line = $"{AssemblyAppIdentitySource.DefaultAppName.ToUpperInvariant()}.Subsystem: ready";

        var result = LoggingPageComponent.PrettyLine(line, colorlessMode: false);

        Assert.DoesNotContain($"{AssemblyAppIdentitySource.DefaultAppName}.", result, StringComparison.Ordinal);
        Assert.DoesNotContain(AssemblyAppIdentitySource.DefaultAppName.ToUpperInvariant(), result, StringComparison.Ordinal);
        Assert.Contains("Subsystem: ready", result);
    }
}
