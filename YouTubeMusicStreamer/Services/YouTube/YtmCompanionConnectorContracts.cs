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

using XeroxDev.YTMDesktop.Companion;
using XeroxDev.YTMDesktop.Companion.Clients;
using XeroxDev.YTMDesktop.Companion.Enums;
using XeroxDev.YTMDesktop.Companion.Models.Output;
using XeroxDev.YTMDesktop.Companion.Settings;

namespace YouTubeMusicStreamer.Services.YouTube;

public interface IYtmRestClient
{
    Task<MetadataOutput?> GetMetadataAsync();
    Task<StateOutput?> GetStateAsync();
    Task<string?> GetAuthCodeAsync();
    Task<string?> GetAuthTokenAsync(string code);
    Task ChangeVideoAsync(string videoId);
    Task NextAsync();
    Task PreviousAsync();
    Task SetVolumeAsync(int volume);
}

public interface IYtmSocketClient
{
    event EventHandler<Exception> Error;
    event EventHandler<ESocketState> ConnectionChanged;
    event EventHandler<StateOutput> PlaybackStateChanged;
    event EventHandler<PlaylistOutput> PlaylistCreated;
    event EventHandler<string> PlaylistDeleted;
    Task ConnectAsync();
}

public interface IYtmCompanionConnector
{
    IYtmRestClient RestClient { get; }
    IYtmSocketClient SocketClient { get; }
    void SetAuthToken(string? token);
}

public interface IYtmCompanionConnectorFactory
{
    IYtmCompanionConnector Create(ConnectorSettings settings);
}

public sealed class YtmCompanionConnectorFactory : IYtmCompanionConnectorFactory
{
    public IYtmCompanionConnector Create(ConnectorSettings settings) =>
        new YtmCompanionConnectorAdapter(new CompanionConnector(settings));
}

internal sealed class YtmCompanionConnectorAdapter(CompanionConnector connector) : IYtmCompanionConnector
{
    private readonly IYtmRestClient _restClient = new YtmRestClientAdapter(connector.RestClient);
    private readonly IYtmSocketClient _socketClient = new YtmSocketClientAdapter(connector.SocketClient);

    public IYtmRestClient RestClient => _restClient;

    public IYtmSocketClient SocketClient => _socketClient;

    public void SetAuthToken(string? token) => connector.SetAuthToken(token ?? string.Empty);
}

internal sealed class YtmRestClientAdapter(RestClient restClient) : IYtmRestClient
{
    public Task<MetadataOutput?> GetMetadataAsync() => restClient.GetMetadata();

    public Task<StateOutput?> GetStateAsync() => restClient.GetState();

    public Task<string?> GetAuthCodeAsync() => restClient.GetAuthCode();

    public Task<string?> GetAuthTokenAsync(string code) => restClient.GetAuthToken(code);

    public Task ChangeVideoAsync(string videoId) => restClient.ChangeVideo(videoId);

    public Task NextAsync() => restClient.Next();

    public Task PreviousAsync() => restClient.Previous();

    public Task SetVolumeAsync(int volume) => restClient.SetVolume(volume);
}

internal sealed class YtmSocketClientAdapter : IYtmSocketClient
{
    private readonly SocketClient _socketClient;

    public YtmSocketClientAdapter(SocketClient socketClient)
    {
        _socketClient = socketClient;
        _socketClient.OnError += HandleError;
        _socketClient.OnConnectionChange += HandleConnectionChanged;
        _socketClient.OnStateChange += HandlePlaybackStateChanged;
        _socketClient.OnPlaylistCreated += HandlePlaylistCreated;
        _socketClient.OnPlaylistDeleted += HandlePlaylistDeleted;
    }

    public event EventHandler<Exception> Error = delegate { };
    public event EventHandler<ESocketState> ConnectionChanged = delegate { };
    public event EventHandler<StateOutput> PlaybackStateChanged = delegate { };
    public event EventHandler<PlaylistOutput> PlaylistCreated = delegate { };
    public event EventHandler<string> PlaylistDeleted = delegate { };

    public Task ConnectAsync() => _socketClient.Connect();

    private void HandleError(object? sender, Exception exception) => Error(this, exception);

    private void HandleConnectionChanged(object? sender, ESocketState state) => ConnectionChanged(this, state);

    private void HandlePlaybackStateChanged(object? sender, StateOutput state) => PlaybackStateChanged(this, state);

    private void HandlePlaylistCreated(object? sender, PlaylistOutput playlist) => PlaylistCreated(this, playlist);

    private void HandlePlaylistDeleted(object? sender, string playlistId) => PlaylistDeleted(this, playlistId);
}
