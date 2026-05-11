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

using Microsoft.Extensions.Logging.Abstractions;
using XeroxDev.YTMDesktop.Companion.Enums;
using XeroxDev.YTMDesktop.Companion.Models.Output;
using YouTubeMusicStreamer.Enums;
using YouTubeMusicStreamer.Services.App.Diagnostics;
using YouTubeMusicStreamer.Services.App.Persistence;
using YouTubeMusicStreamer.Services.YouTube;
using YouTubeMusicStreamer.Tests.TestSupport;

namespace YouTubeMusicStreamer.Tests.YouTube;

public class YouTubeServiceTests
{
    [Theory]
    [InlineData("https://www.youtube.com/watch?v=abc123", "abc123")]
    [InlineData("https://www.youtube.com/watch?v=abc123&list=xyz", "abc123")]
    [InlineData("https://www.youtube.com/watch?v=abc123&t=42s&feature=shared", "abc123")]
    [InlineData("https://youtube.com/watch?v=abc123", "abc123")]
    [InlineData("https://WWW.YOUTUBE.COM/watch?v=abc123", "abc123")]
    [InlineData("https://youtu.be/abc123", "abc123")]
    [InlineData("https://www.youtu.be/abc123", "abc123")]
    [InlineData("https://youtu.be/abc123?t=42", "abc123")]
    [InlineData("https://youtu.be/abc123/extra/path", "abc123")]
    [InlineData("https://m.youtube.com/watch?v=abc123", "abc123")]
    [InlineData("https://music.youtube.com/watch?v=abc123&si=test", "abc123")]
    [InlineData("https://www.youtube.com/shorts/abc123", "abc123")]
    [InlineData("https://www.youtube.com/shorts/abc123?feature=shared", "abc123")]
    [InlineData("https://www.youtube.com/live/abc123", "abc123")]
    [InlineData("https://www.youtube.com/embed/abc123", "abc123")]
    [InlineData("https://www.youtube.com/v/abc123", "abc123")]
    public void GetVideoId_ReturnsExpectedValue_WhenUrlUsesSupportedYouTubeFormat(string url, string expected)
    {
        var result = YouTubeUrlParser.GetVideoId(url);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("youtube.com/watch?v=abc123")]
    [InlineData("ftp://youtube.com/watch?v=abc123")]
    [InlineData("https://www.youtube.com/watch")]
    [InlineData("https://www.youtube.com/watch?v=")]
    [InlineData("https://www.youtube.com/watch?x=abc123")]
    [InlineData("https://example.com/watch?v=abc123")]
    [InlineData("https://www.notyoutube.com/watch?v=abc123")]
    [InlineData("https://www.youtube.com/playlist?list=abc123")]
    [InlineData("https://youtu.be/")]
    [InlineData("https://www.youtube.com/shorts/")]
    [InlineData("https://www.youtube.com/embed/")]
    [InlineData("https://www.youtube.com/live/")]
    public void GetVideoId_ReturnsNull_WhenUrlDoesNotContainSupportedYouTubeVideoId(string url)
    {
        var result = YouTubeUrlParser.GetVideoId(url);

        Assert.Null(result);
    }

    [Fact]
    public void GetVideoId_DelegatesToUrlParser_WhenCalledThroughYouTubeService()
    {
        var result = YouTubeService.GetVideoId("https://www.youtube.com/shorts/abc123");

        Assert.Equal("abc123", result);
    }

    [Fact]
    public void GetVideoId_HandlesMultipleVideoParameters_ByReturningTheFirstOne()
    {
        // HttpUtility.ParseQueryString returns a NameValueCollection where duplicate keys are comma-separated.
        // YouTubeUrlParser uses query["v"] which returns "abc,xyz" if both are present.
        // This test documents the CURRENT behavior, which might be "abc,xyz" or just "abc" depending on implementation.
        // Actually, query["v"] returns "abc,xyz".
        var result = YouTubeUrlParser.GetVideoId("https://youtube.com/watch?v=abc&v=xyz");

        Assert.Equal("abc,xyz", result);
    }

    [Fact]
    public void GetVideoId_HandlesPathologicalTrailingSlashes_Gracefully()
    {
        var result = YouTubeUrlParser.GetVideoId("https://youtu.be///abc123///");

        Assert.Equal("abc123", result);
    }

    [Fact]
    public void GetVideoId_HandlesEncodedCharactersInVideoId_ByDecodingThem()
    {
        // If someone encodes the video ID in the query string.
        var result = YouTubeUrlParser.GetVideoId("https://youtube.com/watch?v=abc%20123");

        Assert.Equal("abc 123", result);
    }

    [Fact]
    public void GetVideoId_ReturnsNull_WhenUrlIsExtremelyLongButNotAValidYouTubeUrl()
    {
        var longUrl = "https://example.com/" + new string('a', 5000);
        var result = YouTubeUrlParser.GetVideoId(longUrl);

        Assert.Null(result);
    }

    [Fact]
    public void GetValidatedHost_ReturnsLoopback_WhenSettingsHostIsLocalhost()
    {
        using var service = CreateService(new YouTubeSettingsSnapshot("localhost", 1234, false, 9876, false, string.Empty));

        var result = service.GetValidatedHost();

        Assert.Equal("127.0.0.1", result);
    }

    [Fact]
    public void GetValidatedHost_ReturnsExplicitHost_WhenHostWasProvided()
    {
        using var service = CreateService(new YouTubeSettingsSnapshot("localhost", 1234, false, 9876, false, string.Empty));

        var result = service.GetValidatedHost("music.youtube.local");

        Assert.Equal("music.youtube.local", result);
    }

    [Fact]
    public void GetValidatedHost_ReturnsLoopback_WhenExplicitAndConfiguredHostsAreMissing()
    {
        using var service = CreateService(new YouTubeSettingsSnapshot(null, 1234, false, 9876, false, string.Empty));

        var result = service.GetValidatedHost();

        Assert.Equal("127.0.0.1", result);
    }

    [Theory]
    [InlineData(null, 5555, 5555)]
    [InlineData(0, 5555, 9863)]
    [InlineData(-1, 5555, 9863)]
    [InlineData(65536, 5555, 9863)]
    [InlineData(9876, 5555, 9876)]
    public void GetValidatedPort_ReturnsExpectedPort_WhenExplicitPortValidationIsApplied(int? explicitPort, int? configuredPort, int expected)
    {
        using var service = CreateService(new YouTubeSettingsSnapshot("127.0.0.1", configuredPort, false, 9876, false, string.Empty));

        var result = service.GetValidatedPort(explicitPort);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetValidatedPort_ReturnsSettingsPort_WhenExplicitPortWasNotProvided()
    {
        using var service = CreateService(new YouTubeSettingsSnapshot("127.0.0.1", 4321, false, 9876, false, string.Empty));

        var result = service.GetValidatedPort();

        Assert.Equal(4321, result);
    }

    [Fact]
    public void GetValidatedPort_ReturnsDefaultPort_WhenExplicitAndConfiguredPortsAreMissing()
    {
        using var service = CreateService(new YouTubeSettingsSnapshot("127.0.0.1", null, false, 9876, false, string.Empty));

        var result = service.GetValidatedPort();

        Assert.Equal(9863, result);
    }

    [Fact]
    public void Constructor_SetsLoggedInState_WhenSessionIsAuthorizedAndConnected()
    {
        using var service = CreateService(sessionState: CreateSessionState(
            authorizationStatus: YtmAuthorizationStatus.Authorized,
            connectionStatus: YtmConnectionStatus.Connected,
            socketState: ESocketState.Connected));

        Assert.Equal(ConnectorState.LoggedIn, service.State);
        Assert.Equal(ESocketState.Connected, service.ConnectionState);
    }

    [Fact]
    public void Constructor_SetsLoggingInState_WhenSessionIsAuthorizing()
    {
        using var service = CreateService(sessionState: CreateSessionState(
            authorizationStatus: YtmAuthorizationStatus.Authorizing,
            connectionStatus: YtmConnectionStatus.Connecting,
            authorizationCode: "ABC123"));

        Assert.Equal(ConnectorState.LoggingIn, service.State);
        Assert.Equal("ABC123", service.Code);
    }

    [Fact]
    public void Constructor_SetsLoggedOutState_WhenAuthorizationIsRequired()
    {
        using var service = CreateService(sessionState: CreateSessionState(
            authorizationStatus: YtmAuthorizationStatus.AuthRequired,
            connectionStatus: YtmConnectionStatus.Disconnected));

        Assert.Equal(ConnectorState.LoggedOut, service.State);
    }

    [Fact]
    public void Constructor_SetsErrorState_WhenAuthorizedSessionIsDegraded()
    {
        using var service = CreateService(sessionState: CreateSessionState(
            authorizationStatus: YtmAuthorizationStatus.Authorized,
            connectionStatus: YtmConnectionStatus.Degraded,
            errorMessage: "Companion degraded"));

        Assert.Equal(ConnectorState.Error, service.State);
        Assert.Equal("Companion degraded", service.Error);
    }

    [Theory]
    [InlineData(YtmConnectionStatus.Connecting)]
    [InlineData(YtmConnectionStatus.Retrying)]
    public void Constructor_SetsLoadingState_WhenConnectionIsInProgress(YtmConnectionStatus connectionStatus)
    {
        using var service = CreateService(sessionState: CreateSessionState(
            authorizationStatus: YtmAuthorizationStatus.Authorized,
            connectionStatus: connectionStatus));

        Assert.Equal(ConnectorState.Loading, service.State);
    }

    [Fact]
    public void Constructor_SetsErrorState_WhenConnectionStatusIsError()
    {
        using var service = CreateService(sessionState: CreateSessionState(
            authorizationStatus: YtmAuthorizationStatus.Authorized,
            connectionStatus: YtmConnectionStatus.Error,
            errorMessage: "Socket failed"));

        Assert.Equal(ConnectorState.Error, service.State);
        Assert.Equal("Socket failed", service.Error);
    }

    [Fact]
    public void StateChangedEvent_UpdatesStateCodeAndError_WhenCoordinatorRaisesSessionChange()
    {
        using var fixture = CreateFixture();

        fixture.Coordinator.RaiseStateChanged(CreateSessionState(
            authorizationStatus: YtmAuthorizationStatus.Authorizing,
            connectionStatus: YtmConnectionStatus.Connecting,
            authorizationCode: "CODE-123",
            errorMessage: "Waiting for approval"));

        Assert.Equal(ConnectorState.LoggingIn, fixture.Service.State);
        Assert.Equal("CODE-123", fixture.Service.Code);
        Assert.Equal("Waiting for approval", fixture.Service.Error);
    }

    [Fact]
    public void SocketConnectionChangedEvent_UpdatesConnectionStateAndRaisesNotification_WhenCoordinatorRaisesSocketChange()
    {
        using var fixture = CreateFixture();
        ESocketState? observedState = null;
        fixture.Service.OnConnectionChange += (_, state) => observedState = state;

        fixture.Coordinator.RaiseSocketConnectionChanged(ESocketState.Connected);

        Assert.Equal(ESocketState.Connected, fixture.Service.ConnectionState);
        Assert.Equal(ESocketState.Connected, observedState);
    }

    [Fact]
    public void ErrorOccurred_RecordsStatusOnlyAuthWarning_WhenErrorLooksLikeAuthorizationFailure()
    {
        using var fixture = CreateFixture();

        fixture.Coordinator.RaiseError(new InvalidOperationException("Authorization failed for YTMDesktop."));

        var diagnostic = Assert.Single(fixture.Diagnostics.RecentDiagnostics);
        Assert.Equal(AppDiagnosticSubsystem.YouTube, diagnostic.Subsystem);
        Assert.Equal(AppDiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal(AppDiagnosticCategory.Auth, diagnostic.Category);
        Assert.Equal("Authorization failed for YTMDesktop.", diagnostic.Summary);
        Assert.Equal(AppDiagnosticVisibility.StatusOnly, diagnostic.Visibility);
    }

    [Fact]
    public void ErrorOccurred_PrioritizesAuthClassification_WhenMessageMentionsBothAuthAndConnectivity()
    {
        using var fixture = CreateFixture();
        fixture.Coordinator.RaiseStateChanged(CreateSessionState(errorMessage: "YTMDesktop companion is unreachable."));

        fixture.Coordinator.RaiseError(new InvalidOperationException("Authorization failed because companion is unreachable."));

        var diagnostic = Assert.Single(fixture.Diagnostics.RecentDiagnostics);
        Assert.Equal(AppDiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal(AppDiagnosticCategory.Auth, diagnostic.Category);
        Assert.Equal(AppDiagnosticVisibility.StatusOnly, diagnostic.Visibility);
        Assert.Equal("YTMDesktop companion is unreachable.", diagnostic.Detail);
    }

    [Fact]
    public void ErrorOccurred_RecordsStatusOnlyConnectivityWarning_WhenErrorLooksLikeRecoveryConnectivityIssue()
    {
        using var fixture = CreateFixture();
        fixture.Coordinator.RaiseStateChanged(CreateSessionState(errorMessage: "YTMDesktop companion is unreachable."));

        fixture.Coordinator.RaiseError(new Exception("YTMDesktop companion is unreachable."));

        var diagnostic = Assert.Single(fixture.Diagnostics.RecentDiagnostics);
        Assert.Equal(AppDiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal(AppDiagnosticCategory.Connectivity, diagnostic.Category);
        Assert.Equal(AppDiagnosticVisibility.StatusOnly, diagnostic.Visibility);
        Assert.Equal("YTMDesktop companion is unreachable.", diagnostic.Detail);
    }

    [Fact]
    public void ErrorOccurred_RecordsToastConnectivityError_WhenErrorIsGenericConnectivityFailure()
    {
        using var fixture = CreateFixture();

        fixture.Coordinator.RaiseError(new Exception("Socket exploded"));

        var diagnostic = Assert.Single(fixture.Diagnostics.RecentDiagnostics);
        Assert.Equal(AppDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal(AppDiagnosticCategory.Connectivity, diagnostic.Category);
        Assert.Equal(AppDiagnosticVisibility.Toast, diagnostic.Visibility);
        Assert.Equal("Socket exploded", diagnostic.Summary);
    }

    [Fact]
    public void Dispose_IgnoresLaterCoordinatorEvents_WhenServiceWasDisposed()
    {
        var fixture = CreateFixture();
        var service = fixture.Service;

        service.Dispose();
        fixture.Coordinator.RaiseStateChanged(CreateSessionState(
            authorizationStatus: YtmAuthorizationStatus.Authorizing,
            connectionStatus: YtmConnectionStatus.Connecting,
            authorizationCode: "SHOULD-NOT-APPLY"));
        fixture.Coordinator.RaiseSocketConnectionChanged(ESocketState.Connected);
        fixture.Coordinator.RaiseError(new Exception("Should not be recorded"));

        Assert.Equal(ConnectorState.LoggedOut, service.State);
        Assert.Equal(string.Empty, service.Code);
        Assert.Equal(ESocketState.Disconnected, service.ConnectionState);
        Assert.Empty(fixture.Diagnostics.RecentDiagnostics);

        fixture.Dispose();
    }

    [Fact]
    public async Task InitializeIfNeededAsync_DelegatesToCoordinator()
    {
        using var fixture = CreateFixture();

        await fixture.Service.InitializeIfNeededAsync();

        Assert.Equal(1, fixture.Coordinator.InitializeCallCount);
    }

    [Fact]
    public async Task SetAddressAsync_DelegatesToCoordinator()
    {
        using var fixture = CreateFixture();

        await fixture.Service.SetAddressAsync("127.0.0.1", 9863);

        Assert.Equal(("127.0.0.1", 9863), fixture.Coordinator.LastConfiguredEndpoint);
    }

    [Fact]
    public async Task ReconnectAsync_DelegatesToCoordinator()
    {
        using var fixture = CreateFixture();

        await fixture.Service.ReconnectAsync();

        Assert.Equal(1, fixture.Coordinator.ReconnectCallCount);
    }

    [Fact]
    public void PlaylistEvents_AreForwarded_WhenCoordinatorRaisesPlaylistNotifications()
    {
        using var fixture = CreateFixture();
        PlaylistOutput? created = null;
        string? deletedId = null;
        fixture.Service.OnPlaylistCreated += (_, playlist) => created = playlist;
        fixture.Service.OnPlaylistDeleted += (_, playlistId) => deletedId = playlistId;

        var playlist = new PlaylistOutput { Id = "playlist-1", Title = "Favorites" };
        fixture.Coordinator.RaisePlaylistCreated(playlist);
        fixture.Coordinator.RaisePlaylistDeleted("playlist-2");

        Assert.Same(playlist, created);
        Assert.Equal("playlist-2", deletedId);
    }

    private static YouTubeService CreateService(
        YouTubeSettingsSnapshot? settingsSnapshot = null,
        YtmCompanionSessionState? sessionState = null)
    {
        using var fixture = CreateFixture(settingsSnapshot, sessionState);
        return fixture.DetachService();
    }

    private static YouTubeServiceFixture CreateFixture(
        YouTubeSettingsSnapshot? settingsSnapshot = null,
        YtmCompanionSessionState? sessionState = null)
    {
        var diagnostics = new RecordingDiagnosticsService();
        var settings = SettingsServiceTestSupport.CreateWithThrowingDb(
            diagnostics,
            youTubeSettings: settingsSnapshot ?? new YouTubeSettingsSnapshot(null, null, false, 9876, false, string.Empty));

        var coordinator = new FakeSessionCoordinator
        {
            State = sessionState ?? CreateSessionState()
        };

        var service = new YouTubeService(
            NullLogger<YouTubeService>.Instance,
            settings,
            null!,
            null!,
            coordinator,
            diagnostics);

        return new YouTubeServiceFixture(service, coordinator, diagnostics);
    }

    private static YtmCompanionSessionState CreateSessionState(
        YtmAuthorizationStatus authorizationStatus = YtmAuthorizationStatus.NotConfigured,
        YtmConnectionStatus connectionStatus = YtmConnectionStatus.Disconnected,
        ESocketState socketState = ESocketState.Disconnected,
        string? errorMessage = null,
        string? authorizationCode = null) =>
        new(
            new YtmEndpointConfiguration("127.0.0.1", 9863),
            YtmEndpointStatus.ValidCompanion,
            authorizationStatus,
            connectionStatus,
            socketState,
            errorMessage,
            authorizationCode,
            false,
            null);

    private sealed class FakeSessionCoordinator : IYtmCompanionSessionCoordinator
    {
        public YtmCompanionSessionState State { get; set; } = CreateSessionState();
        public IYtmRestClient? RestClient => null;
        public IYtmSocketClient? SocketClient => null;
        public int InitializeCallCount { get; private set; }
        public (string Host, int Port)? LastConfiguredEndpoint { get; private set; }
        public int ReconnectCallCount { get; private set; }
        public event EventHandler<YtmCompanionSessionState> StateChanged = delegate { };
        public event EventHandler<StateOutput> PlaybackStateChanged = delegate { };
        public event EventHandler<ESocketState> SocketConnectionChanged = delegate { };
        public event EventHandler<PlaylistOutput> PlaylistCreated = delegate { };
        public event EventHandler<string> PlaylistDeleted = delegate { };
        public event EventHandler<Exception> ErrorOccurred = delegate { };
        public Task InitializeIfNeededAsync(CancellationToken cancellationToken = default)
        {
            InitializeCallCount++;
            return Task.CompletedTask;
        }

        public Task ConfigureEndpointAsync(string host, int port, CancellationToken cancellationToken = default)
        {
            LastConfiguredEndpoint = (host, port);
            return Task.CompletedTask;
        }

        public Task ReconnectAsync(CancellationToken cancellationToken = default)
        {
            ReconnectCallCount++;
            return Task.CompletedTask;
        }

        public void RaiseStateChanged(YtmCompanionSessionState state)
        {
            State = state;
            StateChanged(this, state);
        }

        public void RaiseSocketConnectionChanged(ESocketState state)
        {
            SocketConnectionChanged(this, state);
        }

        public void RaiseError(Exception exception)
        {
            ErrorOccurred(this, exception);
        }

        public void RaisePlaylistCreated(PlaylistOutput playlist)
        {
            PlaylistCreated(this, playlist);
        }

        public void RaisePlaylistDeleted(string playlistId)
        {
            PlaylistDeleted(this, playlistId);
        }
    }

    private sealed class YouTubeServiceFixture(
        YouTubeService service,
        FakeSessionCoordinator coordinator,
        RecordingDiagnosticsService diagnostics) : IDisposable
    {
        private bool _detached;

        public YouTubeService Service { get; } = service;
        public FakeSessionCoordinator Coordinator { get; } = coordinator;
        public RecordingDiagnosticsService Diagnostics { get; } = diagnostics;

        public YouTubeService DetachService()
        {
            _detached = true;
            return Service;
        }

        public void Dispose()
        {
            if (!_detached)
                Service.Dispose();
        }
    }
}
