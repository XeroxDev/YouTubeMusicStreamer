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

using YouTubeMusicStreamer.Components.Pages.YTMDesktop;

namespace YouTubeMusicStreamer.Tests.Components.Pages.YTMDesktop;

public sealed class YtmDesktopBlacklistStateTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryCreateEntry_RejectsMissingUrl(string? url)
    {
        var result = YtmDesktopBlacklistState.TryCreateEntry(url, "blocked");

        Assert.False(result.IsValid);
        Assert.Equal(BlacklistEntryValidationError.MissingUrl, result.Error);
        Assert.Null(result.Entry);
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("/watch?v=abc123")]
    [InlineData("https://")]
    public void TryCreateEntry_RejectsMalformedOrRelativeUrl(string url)
    {
        var result = YtmDesktopBlacklistState.TryCreateEntry(url, "blocked");

        Assert.False(result.IsValid);
        Assert.Equal(BlacklistEntryValidationError.InvalidUrl, result.Error);
        Assert.Null(result.Entry);
    }

    [Fact]
    public void TryCreateEntry_TrimsUrlAndKeepsDescription()
    {
        var result = YtmDesktopBlacklistState.TryCreateEntry("  https://youtu.be/abc123  ", "blocked by streamer");

        Assert.True(result.IsValid);
        Assert.NotNull(result.Entry);
        Assert.Equal("https://youtu.be/abc123", result.Entry.Url.ToString().TrimEnd('/'));
        Assert.Equal("blocked by streamer", result.Entry.Description);
    }

    [Fact]
    public void TryCreateEntry_TreatsNullDescriptionAsEmpty()
    {
        var result = YtmDesktopBlacklistState.TryCreateEntry("https://www.youtube.com/watch?v=abc123", null);

        Assert.True(result.IsValid);
        Assert.NotNull(result.Entry);
        Assert.Equal(string.Empty, result.Entry.Description);
    }
}
