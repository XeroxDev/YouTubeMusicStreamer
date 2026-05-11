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

using YouTubeMusicStreamer.Components;
using YouTubeMusicStreamer.Components.Pages.Home.Components;
using YouTubeMusicStreamer.Services.YouTube;

namespace YouTubeMusicStreamer.Tests.Components.Pages.Home;

public sealed class YouTubeConnectionViewStateTests
{
    [Theory]
    [InlineData("localhost", 9863, "LOCALHOST", 9863, YtmAuthorizationStatus.Authorized, true)]
    [InlineData("localhost", 9863, "127.0.0.1", 9863, YtmAuthorizationStatus.Authorized, false)]
    [InlineData("localhost", 9864, "localhost", 9863, YtmAuthorizationStatus.Authorized, false)]
    [InlineData("localhost", 9863, "localhost", 9863, YtmAuthorizationStatus.AuthRequired, false)]
    [InlineData("localhost", 9863, "localhost", 9863, YtmAuthorizationStatus.AuthFailed, false)]
    public void ShouldReconnect_RequiresAuthorizedUnchangedEndpoint(
        string enteredHost,
        int enteredPort,
        string currentHost,
        int currentPort,
        YtmAuthorizationStatus authorizationStatus,
        bool expected)
    {
        var shouldReconnect = YouTubeConnectionViewState.ShouldReconnect(
            enteredHost,
            enteredPort,
            currentHost,
            currentPort,
            authorizationStatus);

        Assert.Equal(expected, shouldReconnect);
    }

    [Theory]
    [InlineData(YtmEndpointStatus.ValidCompanion, (int)UiStatusTone.Success)]
    [InlineData(YtmEndpointStatus.Unreachable, (int)UiStatusTone.Warning)]
    [InlineData(YtmEndpointStatus.IncompatibleEndpoint, (int)UiStatusTone.Danger)]
    [InlineData(YtmEndpointStatus.NotConfigured, (int)UiStatusTone.Neutral)]
    [InlineData(YtmEndpointStatus.Unknown, (int)UiStatusTone.Info)]
    public void EndpointStatusSeverity_IsStable(YtmEndpointStatus status, int tone)
    {
        Assert.Equal((UiStatusTone)tone, YouTubeConnectionViewState.GetEndpointStatusTone(status));
    }

    [Theory]
    [InlineData(YtmAuthorizationStatus.NotConfigured, (int)UiStatusTone.Neutral)]
    [InlineData(YtmAuthorizationStatus.AuthRequired, (int)UiStatusTone.Warning)]
    [InlineData(YtmAuthorizationStatus.Authorizing, (int)UiStatusTone.Info)]
    [InlineData(YtmAuthorizationStatus.Authorized, (int)UiStatusTone.Success)]
    [InlineData(YtmAuthorizationStatus.AuthFailed, (int)UiStatusTone.Danger)]
    public void AuthorizationStatusSeverity_IsStable(YtmAuthorizationStatus status, int tone)
    {
        Assert.Equal((UiStatusTone)tone, YouTubeConnectionViewState.GetAuthorizationStatusTone(status));
    }

    [Theory]
    [InlineData(YtmConnectionStatus.Disconnected, (int)UiStatusTone.Neutral)]
    [InlineData(YtmConnectionStatus.Connecting, (int)UiStatusTone.Info)]
    [InlineData(YtmConnectionStatus.Connected, (int)UiStatusTone.Success)]
    [InlineData(YtmConnectionStatus.Retrying, (int)UiStatusTone.Info)]
    [InlineData(YtmConnectionStatus.Degraded, (int)UiStatusTone.Warning)]
    [InlineData(YtmConnectionStatus.Error, (int)UiStatusTone.Danger)]
    public void ConnectionStatusSeverity_IsStable(YtmConnectionStatus status, int tone)
    {
        Assert.Equal((UiStatusTone)tone, YouTubeConnectionViewState.GetConnectionStatusTone(status));
    }
}
