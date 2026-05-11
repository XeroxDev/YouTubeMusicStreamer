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
using NuGet.Versioning;
using System.Net.Http;
using XeroxDev.YTMDesktop.Companion.Enums;
using XeroxDev.YTMDesktop.Companion.Models.Output;
using XeroxDev.YTMDesktop.Companion.Settings;
using YouTubeMusicStreamer.Services.App;
using YouTubeMusicStreamer.Services.YouTube;
using YouTubeMusicStreamer.Tests.TestSupport;

namespace YouTubeMusicStreamer.Tests.Services.YouTube;

public sealed class YtmCompanionSessionCoordinatorTests
{
    [Fact]
    public async Task InitializeIfNeededAsync_SetsNotConfigured_WhenEndpointIsMissing()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        await harness.Settings.InitializeAsync();

        using var coordinator = CreateCoordinator(harness.Settings, out _);

        await coordinator.InitializeIfNeededAsync();

        Assert.Equal(YtmEndpointStatus.NotConfigured, coordinator.State.EndpointStatus);
        Assert.Equal(YtmAuthorizationStatus.NotConfigured, coordinator.State.AuthorizationStatus);
        Assert.Equal(YtmConnectionStatus.Disconnected, coordinator.State.ConnectionStatus);
        Assert.False(coordinator.State.HasStoredToken);
    }

    [Fact]
    public async Task InitializeIfNeededAsync_SetsAuthRequired_WhenEndpointExistsButNoStoredToken()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        await harness.Settings.InitializeAsync();
        await harness.Settings.SaveYouTubeSettingsAsync(new("localhost", 9863, false, 9876, false, string.Empty));

        using var coordinator = CreateCoordinator(harness.Settings, out _);

        await coordinator.InitializeIfNeededAsync();

        Assert.Equal("127.0.0.1", coordinator.State.Endpoint.Host);
        Assert.Equal(9863, coordinator.State.Endpoint.Port);
        Assert.Equal(YtmAuthorizationStatus.AuthRequired, coordinator.State.AuthorizationStatus);
        Assert.Equal(YtmConnectionStatus.Disconnected, coordinator.State.ConnectionStatus);
        Assert.False(coordinator.State.HasStoredToken);
    }

    [Fact]
    public async Task InitializeIfNeededAsync_SetsError_WhenEndpointRespondsWithIncompatibleMetadata()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        await harness.Settings.InitializeAsync();
        await harness.Settings.SaveYouTubeSettingsAsync(new("127.0.0.1", 9863, false, 9876, false, string.Empty));
        await harness.Settings.SaveSensitiveSettingAsync(s => s.YtmDesktopToken = "stored-token");

        using var coordinator = CreateCoordinator(
            harness.Settings,
            out var factory,
            configureConnector: connector => connector.RestClient.Metadata = null);

        await coordinator.InitializeIfNeededAsync();

        Assert.Equal(YtmEndpointStatus.IncompatibleEndpoint, coordinator.State.EndpointStatus);
        Assert.Equal(YtmConnectionStatus.Error, coordinator.State.ConnectionStatus);
        Assert.Equal("The configured endpoint is not a compatible YTMDesktop companion.", coordinator.State.ErrorMessage);
        Assert.True(coordinator.State.HasStoredToken);
        Assert.Single(factory.CreatedConnectors);
        Assert.Equal(0, factory.CreatedConnectors[0].SocketClient.ConnectCallCount);
    }

    [Fact]
    public async Task InitializeIfNeededAsync_ConnectsSuccessfully_WhenStoredTokenEndpointIsHealthy()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        await harness.Settings.InitializeAsync();
        await harness.Settings.SaveYouTubeSettingsAsync(new("127.0.0.1", 9863, false, 9876, false, string.Empty));
        await harness.Settings.SaveSensitiveSettingAsync(s => s.YtmDesktopToken = "stored-token");

        using var coordinator = CreateCoordinator(
            harness.Settings,
            out var factory,
            configureConnector: connector =>
            {
                connector.RestClient.Metadata = new MetadataOutput();
                connector.SocketClient.ConnectBehavior = socket =>
                {
                    socket.RaiseConnectionChanged(ESocketState.Connecting);
                    socket.RaiseConnectionChanged(ESocketState.Connected);
                    return Task.CompletedTask;
                };
            });

        await coordinator.InitializeIfNeededAsync();

        Assert.Equal(YtmEndpointStatus.ValidCompanion, coordinator.State.EndpointStatus);
        Assert.Equal(YtmAuthorizationStatus.Authorized, coordinator.State.AuthorizationStatus);
        Assert.Equal(YtmConnectionStatus.Connected, coordinator.State.ConnectionStatus);
        Assert.Equal(ESocketState.Connected, coordinator.State.SocketState);
        Assert.True(coordinator.State.HasStoredToken);
        Assert.Equal(1, factory.CreatedConnectors[0].SocketClient.ConnectCallCount);
    }

    [Fact]
    public async Task ConfigureEndpointAsync_ClearsOldTokenAndPersistsNewToken_WhenEndpointChangesAndAuthSucceeds()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        await harness.Settings.InitializeAsync();
        await harness.Settings.SaveYouTubeSettingsAsync(new("127.0.0.1", 9863, false, 9876, false, string.Empty));
        await harness.Settings.SaveSensitiveSettingAsync(s => s.YtmDesktopToken = "old-token");

        using var coordinator = CreateCoordinator(
            harness.Settings,
            out var factory,
            configureConnector: connector =>
            {
                connector.RestClient.Metadata = new MetadataOutput();
                connector.RestClient.AuthCode = "code-123";
                connector.RestClient.AuthToken = "new-token";
                connector.SocketClient.ConnectBehavior = socket =>
                {
                    socket.RaiseConnectionChanged(ESocketState.Connected);
                    return Task.CompletedTask;
                };
            });

        await coordinator.ConfigureEndpointAsync("localhost", 4321);

        var settings = harness.Settings.GetYouTubeSettings();
        var sensitive = await harness.Settings.GetSensitiveSettingsAsync();
        var createdConnector = Assert.Single(factory.CreatedConnectors);

        Assert.Equal("127.0.0.1", settings.Host);
        Assert.Equal(4321, settings.Port);
        Assert.Equal("new-token", sensitive.YtmDesktopToken);
        Assert.Equal(["new-token"], createdConnector.SetAuthTokenCalls);
        Assert.Equal("new-token", createdConnector.RestClient.Settings.Token);
        Assert.Equal(YtmAuthorizationStatus.Authorized, coordinator.State.AuthorizationStatus);
        Assert.Equal(YtmConnectionStatus.Connected, coordinator.State.ConnectionStatus);
        Assert.True(coordinator.State.HasStoredToken);
    }

    [Fact]
    public async Task ConfigureEndpointAsync_LeavesStoredTokenCleared_WhenEndpointChangesButAuthorizationFails()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        await harness.Settings.InitializeAsync();
        await harness.Settings.SaveYouTubeSettingsAsync(new("127.0.0.1", 9863, false, 9876, false, string.Empty));
        await harness.Settings.SaveSensitiveSettingAsync(s => s.YtmDesktopToken = "old-token");

        using var coordinator = CreateCoordinator(
            harness.Settings,
            out var factory,
            configureConnector: connector =>
            {
                connector.RestClient.Metadata = new MetadataOutput();
                connector.RestClient.AuthCode = string.Empty;
            });

        await coordinator.ConfigureEndpointAsync("127.0.0.1", 4321);

        var sensitive = await harness.Settings.GetSensitiveSettingsAsync();
        var createdConnector = Assert.Single(factory.CreatedConnectors);

        Assert.Null(sensitive.YtmDesktopToken);
        Assert.Empty(createdConnector.SetAuthTokenCalls);
        Assert.Null(createdConnector.RestClient.Settings.Token);
        Assert.Equal(YtmAuthorizationStatus.AuthFailed, coordinator.State.AuthorizationStatus);
        Assert.Equal(YtmConnectionStatus.Error, coordinator.State.ConnectionStatus);
        Assert.False(coordinator.State.HasStoredToken);
    }

    [Fact]
    public async Task InitializeIfNeededAsync_EntersRetryingRecovery_WhenHealthyStoredTokenEndpointBecomesUnreachable()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        await harness.Settings.InitializeAsync();
        await harness.Settings.SaveYouTubeSettingsAsync(new("127.0.0.1", 9863, false, 9876, false, string.Empty));
        await harness.Settings.SaveSensitiveSettingAsync(s => s.YtmDesktopToken = "stored-token");

        using var coordinator = CreateCoordinator(
            harness.Settings,
            out _,
            configureConnector: connector =>
            {
                connector.RestClient.Metadata = new MetadataOutput();
                connector.SocketClient.ConnectBehavior = socket =>
                {
                    socket.RaiseConnectionChanged(ESocketState.Connected);
                    return Task.CompletedTask;
                };
            });

        await coordinator.InitializeIfNeededAsync();
        ((FakeYtmSocketClient)coordinator.SocketClient!).RaiseError(new HttpRequestException("Connection refused"));

        Assert.Equal(YtmConnectionStatus.Retrying, coordinator.State.ConnectionStatus);
        Assert.Equal(YtmEndpointStatus.Unreachable, coordinator.State.EndpointStatus);
        Assert.Equal("YTMDesktop companion is unreachable. Reconnecting automatically.", coordinator.State.ErrorMessage);
    }

    private static YtmCompanionSessionCoordinator CreateCoordinator(
        SettingsService settings,
        out FakeYtmConnectorFactory factory,
        Action<FakeYtmCompanionConnector>? configureConnector = null)
    {
        factory = new FakeYtmConnectorFactory(configureConnector);
        return new YtmCompanionSessionCoordinator(
            NullLogger<YtmCompanionSessionCoordinator>.Instance,
            settings,
            new FakeAppIdentitySource(),
            new FakeAppVersionSource(),
            factory);
    }

    private sealed class FakeAppIdentitySource : IAppIdentitySource
    {
        public string AppName => "YouTube Music Streamer";
        public string AppId => "youtube-music-streamer";
    }

    private sealed class FakeAppVersionSource : IAppVersionSource
    {
        public SemanticVersion GetInternalAppVersion() => new(1, 2, 3);
    }

    private sealed class FakeYtmConnectorFactory(Action<FakeYtmCompanionConnector>? configureConnector) : IYtmCompanionConnectorFactory
    {
        public List<FakeYtmCompanionConnector> CreatedConnectors { get; } = [];

        public IYtmCompanionConnector Create(ConnectorSettings settings)
        {
            var connector = new FakeYtmCompanionConnector(settings);
            configureConnector?.Invoke(connector);
            CreatedConnectors.Add(connector);
            return connector;
        }
    }

    private sealed class FakeYtmCompanionConnector(ConnectorSettings settings) : IYtmCompanionConnector
    {
        public FakeYtmRestClient RestClient { get; } = new(settings);
        public FakeYtmSocketClient SocketClient { get; } = new();
        public List<string> SetAuthTokenCalls { get; } = [];

        IYtmRestClient IYtmCompanionConnector.RestClient => RestClient;
        IYtmSocketClient IYtmCompanionConnector.SocketClient => SocketClient;

        public void SetAuthToken(string? token)
        {
            var normalized = token ?? string.Empty;
            SetAuthTokenCalls.Add(normalized);
            RestClient.Settings.Token = normalized;
        }
    }

    private sealed class FakeYtmRestClient(ConnectorSettings settings) : IYtmRestClient
    {
        public ConnectorSettings Settings { get; } = settings;
        public MetadataOutput? Metadata { get; set; } = new();
        public Exception? MetadataException { get; set; }
        public string? AuthCode { get; set; }
        public string? AuthToken { get; set; }

        public Task<MetadataOutput?> GetMetadataAsync()
        {
            if (MetadataException is not null)
                return Task.FromException<MetadataOutput?>(MetadataException);

            return Task.FromResult(Metadata);
        }

        public Task<StateOutput?> GetStateAsync() => Task.FromResult<StateOutput?>(null);

        public Task<string?> GetAuthCodeAsync() => Task.FromResult(AuthCode);

        public Task<string?> GetAuthTokenAsync(string code) => Task.FromResult(AuthToken);

        public Task ChangeVideoAsync(string videoId) => Task.CompletedTask;

        public Task NextAsync() => Task.CompletedTask;

        public Task PreviousAsync() => Task.CompletedTask;

        public Task SetVolumeAsync(int volume) => Task.CompletedTask;
    }

    private sealed class FakeYtmSocketClient : IYtmSocketClient
    {
        public event EventHandler<Exception> Error = delegate { };
        public event EventHandler<ESocketState> ConnectionChanged = delegate { };
        public event EventHandler<StateOutput> PlaybackStateChanged = delegate { };
        public event EventHandler<PlaylistOutput> PlaylistCreated = delegate { };
        public event EventHandler<string> PlaylistDeleted = delegate { };

        public int ConnectCallCount { get; private set; }
        public Func<FakeYtmSocketClient, Task>? ConnectBehavior { get; set; }

        public async Task ConnectAsync()
        {
            ConnectCallCount++;
            if (ConnectBehavior is not null)
            {
                await ConnectBehavior(this);
                return;
            }

            RaiseConnectionChanged(ESocketState.Connected);
        }

        public void RaiseError(Exception exception) => Error(this, exception);

        public void RaiseConnectionChanged(ESocketState state) => ConnectionChanged(this, state);

        public void RaisePlaybackStateChanged(StateOutput state) => PlaybackStateChanged(this, state);

        public void RaisePlaylistCreated(PlaylistOutput playlist) => PlaylistCreated(this, playlist);

        public void RaisePlaylistDeleted(string playlistId) => PlaylistDeleted(this, playlistId);
    }
}
