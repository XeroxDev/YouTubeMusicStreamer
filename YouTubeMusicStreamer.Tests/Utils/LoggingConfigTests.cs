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
using Serilog.Events;
using Serilog.Parsing;
using YouTubeMusicStreamer.Utils;

namespace YouTubeMusicStreamer.Tests.Utils;

public sealed class LoggingConfigTests
{
    [Fact]
    public void ShouldWriteEvent_AllowsEverything_WhenRuntimeLevelIsNotInformation()
    {
        var frameworkDebug = CreateEvent(LogEventLevel.Debug, "Microsoft.AspNetCore.Components.RenderTree");

        Assert.True(LoggingConfig.LoggingEventFilter.ShouldWriteEvent(frameworkDebug, LogLevel.Debug));
        Assert.True(LoggingConfig.LoggingEventFilter.ShouldWriteEvent(frameworkDebug, LogLevel.Warning));
    }

    [Fact]
    public void ShouldWriteEvent_AtInformation_AllowsAppCategories()
    {
        Assert.True(LoggingConfig.LoggingEventFilter.ShouldWriteEvent(
            CreateEvent(LogEventLevel.Debug, "YouTubeMusicStreamer.Services.Twitch.TwitchService"),
            LogLevel.Information));
        Assert.True(LoggingConfig.LoggingEventFilter.ShouldWriteEvent(
            CreateEvent(LogEventLevel.Information, "Program"),
            LogLevel.Information));
    }

    [Fact]
    public void ShouldWriteEvent_AtInformation_FiltersLowLevelFrameworkNoise()
    {
        Assert.False(LoggingConfig.LoggingEventFilter.ShouldWriteEvent(
            CreateEvent(LogEventLevel.Debug, "Microsoft.AspNetCore.Components.RenderTree"),
            LogLevel.Information));
        Assert.False(LoggingConfig.LoggingEventFilter.ShouldWriteEvent(
            CreateEvent(LogEventLevel.Information, "Microsoft.Extensions.Http.DefaultHttpClientFactory"),
            LogLevel.Information));
    }

    [Fact]
    public void ShouldWriteEvent_AtInformation_KeepsFrameworkWarningsAndErrors()
    {
        Assert.True(LoggingConfig.LoggingEventFilter.ShouldWriteEvent(
            CreateEvent(LogEventLevel.Warning, "Microsoft.AspNetCore.Components.RenderTree"),
            LogLevel.Information));
        Assert.True(LoggingConfig.LoggingEventFilter.ShouldWriteEvent(
            CreateEvent(LogEventLevel.Error, "System.Net.Http.HttpClient"),
            LogLevel.Information));
    }

    [Fact]
    public void ShouldWriteEvent_AtInformation_KeepsEventsWithoutUsableSourceContext()
    {
        Assert.True(LoggingConfig.LoggingEventFilter.ShouldWriteEvent(
            CreateEvent(LogEventLevel.Information, null),
            LogLevel.Information));
        Assert.True(LoggingConfig.LoggingEventFilter.ShouldWriteEvent(
            CreateEvent(LogEventLevel.Information, "   "),
            LogLevel.Information));
    }

    private static LogEvent CreateEvent(LogEventLevel level, string? sourceContext)
    {
        var properties = new List<LogEventProperty>();
        if (sourceContext is not null)
            properties.Add(new LogEventProperty("SourceContext", new ScalarValue(sourceContext)));

        return new LogEvent(
            DateTimeOffset.UtcNow,
            level,
            null,
            new MessageTemplateParser().Parse("Test message"),
            properties);
    }
}
