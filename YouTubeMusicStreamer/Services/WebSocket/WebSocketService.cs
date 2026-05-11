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

using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using YouTubeMusicStreamer.Services.App;
using YouTubeMusicStreamer.Services.App.Persistence;

namespace YouTubeMusicStreamer.Services.WebSocket;

public enum SocketEvents
{
    TrackInfo,
    AudioInfo
}

public partial class WebSocketService(
    SettingsService settingsService,
    IAudioCaptureService audioService,
    IWidgetServerHost widgetServerHost,
    IAsyncDelay delay) : IWidgetServerController, IDisposable
{
    private const int SilenceFrameSampleCount = 1024;
    private CancellationTokenSource? _cts;
    private Task? _acceptLoopTask;
    private readonly List<IWidgetClientConnection> _clients = [];
    private string? _listenerPrefix;
    private object? _latestTrackInfo;
    private WidgetServerConfiguration _configuration = new(
        settingsService.GetYouTubeSettings().PublicPort,
        settingsService.GetYouTubeSettings().AllowAudioCapture,
        settingsService.GetYouTubeSettings().AudioCaptureDevice);

    public WidgetServerState State { get; private set; } = new(
        new WidgetServerConfiguration(
            settingsService.GetYouTubeSettings().PublicPort,
            settingsService.GetYouTubeSettings().AllowAudioCapture,
            settingsService.GetYouTubeSettings().AudioCaptureDevice),
        WidgetServerStatus.Stopped,
        0,
        null,
        audioService.State,
        null);

    public event EventHandler<WidgetServerState> StateChanged = delegate { };

    public bool IsRunning => widgetServerHost.IsListening;

    public async Task StartAsync()
    {
        if (widgetServerHost.IsListening)
            return;

        var savedSettings = settingsService.GetYouTubeSettings();
        await StartAsync(new WidgetServerConfiguration(
            savedSettings.PublicPort,
            savedSettings.AllowAudioCapture,
            savedSettings.AudioCaptureDevice));
    }

    private async Task StartAsync(WidgetServerConfiguration configuration)
    {
        if (widgetServerHost.IsListening)
            return;

        _configuration = configuration;
        UpdateState(State with
        {
            Configuration = _configuration,
            AudioState = audioService.State,
            ErrorMessage = null
        });

        UpdateState(State with { Status = WidgetServerStatus.Starting, ErrorMessage = null });
        _listenerPrefix = $"http://localhost:{_configuration.Port}/";

        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        widgetServerHost.Start(_listenerPrefix);

        audioService.DataAvailable += BroadcastAudioData;
        audioService.AudioInfoChanged += BroadcastAudioInfo;
        audioService.StateChanged += HandleAudioStateChanged;
        await audioService.ApplyConfigurationAsync(_configuration.AudioEnabled, _configuration.AudioDeviceId, _cts.Token);

        _acceptLoopTask = Task.Run(() => AcceptLoopAsync(_cts.Token), _cts.Token);
        UpdateState(State with { Status = ComputeStatus(IsRunning, audioService.State, null) });
    }

    public async Task ApplyConfigurationAsync(YouTubeSettingsSnapshot settings, CancellationToken cancellationToken = default)
    {
        var nextConfiguration = new WidgetServerConfiguration(
            settings.PublicPort,
            settings.AllowAudioCapture,
            settings.AudioCaptureDevice);

        var portChanged = nextConfiguration.Port != _configuration.Port;
        _configuration = nextConfiguration;
        UpdateState(State with { Configuration = _configuration, ErrorMessage = null });

        if (IsRunning && portChanged)
        {
            Stop();
            await StartAsync(nextConfiguration);
            return;
        }

        await audioService.ApplyConfigurationAsync(_configuration.AudioEnabled, _configuration.AudioDeviceId, cancellationToken);
        UpdateState(State with { Status = ComputeStatus(IsRunning, audioService.State, State.ErrorMessage) });
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            IWidgetServerRequest? request;
            try
            {
                request = await widgetServerHost.AcceptAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            if (request.IsWebSocketRequest)
            {
                var client = await request.AcceptWebSocketAsync(cancellationToken);

                lock (_clients)
                {
                    _clients.Add(client);
                    UpdateState(State with { ConnectedClients = _clients.Count });
                }

                _ = Task.Run(() => MonitorClientAsync(client));
                _ = Task.Run(() => OnClientConnectMessage(client));
            }
            else
            {
                request.RejectBadRequest();
            }
        }
    }

    private async Task MonitorClientAsync(IWidgetClientConnection client)
    {
        var buffer = new byte[1024];
        try
        {
            while (client.State == WebSocketState.Open)
            {
                if (_cts is null) continue;
                var result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                if (result.MessageType == WebSocketMessageType.Close)
                    break;
            }
        }
        catch
        {
            // ignored
        }

        lock (_clients)
        {
            _clients.Remove(client);
            UpdateState(State with { ConnectedClients = _clients.Count });
        }

        try
        {
            await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed", CancellationToken.None);
        }
        catch
        {
            // ignored
        }
    }

    private async Task OnClientConnectMessage(IWidgetClientConnection client)
    {
        if (_cts?.Token is null) return;

        if (audioService.CurrentAudioInfo is { } audioInfo)
            await client.SendAsync(CreateJsonMessage(SocketEvents.AudioInfo, audioInfo), WebSocketMessageType.Text, _cts.Token);

        if (_latestTrackInfo is not null)
            await client.SendAsync(CreateJsonMessage(SocketEvents.TrackInfo, _latestTrackInfo), WebSocketMessageType.Text, _cts.Token);
    }

    private static byte[] CreateJsonMessage(SocketEvents socketEvent, object message)
    {
        // get event name as string
        var e = Enum.GetName(socketEvent);
        if (e is null) return [];
        var obj = new
        {
            e,
            data = message
        };

        return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(obj));
    }

    public async Task BroadcastMessageAsync(SocketEvents socketEvent, object message, CancellationToken token = default) =>
        await BroadcastMessageAsync(CreateJsonMessage(socketEvent, message), WebSocketMessageType.Text, token);

    public async Task BroadcastTrackInfoAsync(object message, CancellationToken token = default)
    {
        _latestTrackInfo = message;
        UpdateState(State with { LastTrackInfo = message });
        await BroadcastMessageAsync(SocketEvents.TrackInfo, message, token);
    }

    private async Task BroadcastMessageAsync(byte[] data, WebSocketMessageType messageType, CancellationToken token = default)
    {
        List<IWidgetClientConnection> clientsCopy;
        lock (_clients)
        {
            clientsCopy = _clients.ToList();
        }

        foreach (var client in clientsCopy.Where(client => client.State == WebSocketState.Open))
        {
            try
            {
                await client.SendAsync(data, messageType, token);
            }
            catch
            {
                // ignored
            }
        }
    }

    private byte[]? _latestAudioData;
    private readonly object _audioLock = new();
    private bool _isBroadcasting;

    private void BroadcastAudioData(object? sender, byte[] e)
    {
        lock (_audioLock)
        {
            // Always update with the most recent data.
            _latestAudioData = e;
            if (_isBroadcasting) return;
            _isBroadcasting = true;
            // Start a broadcasting loop on a background task.
            _ = Task.Run(BroadcastLoopAsync);
        }
    }

    private async Task BroadcastLoopAsync()
    {
        while (true)
        {
            byte[]? dataToSend;
            lock (_audioLock)
            {
                // Capture the latest audio data and reset the field.
                dataToSend = _latestAudioData;
                _latestAudioData = null;
            }

            if (dataToSend != null)
            {
                await BroadcastMessageAsync(dataToSend, WebSocketMessageType.Binary);
            }

            // Wait for ~33ms (about 30Hz)
            await delay.DelayAsync(TimeSpan.FromMilliseconds(33));

            lock (_audioLock)
            {
                // If no new data has arrived, exit the loop.
                if (_latestAudioData != null) continue;
                _isBroadcasting = false;
                break;
            }
        }
    }

    private void BroadcastAudioInfo(object? sender, AudioInfo e)
    {
        _ = BroadcastMessageAsync(SocketEvents.AudioInfo, e);
    }

    private void HandleAudioStateChanged(object? sender, AudioSubsystemState audioState)
    {
        var previousAudioState = State.AudioState;
        UpdateState(State with
        {
            AudioState = audioState,
            Status = ComputeStatus(IsRunning, audioState, State.ErrorMessage)
        });

        if (audioState.Status != AudioCaptureStatus.Capturing &&
            (previousAudioState.Status == AudioCaptureStatus.Capturing || HasBufferedAudioData()))
        {
            _ = BroadcastSilenceFrameAsync();
        }
    }

    public void Stop()
    {
        audioService.StopCapture();
        audioService.DataAvailable -= BroadcastAudioData;
        audioService.AudioInfoChanged -= BroadcastAudioInfo;
        audioService.StateChanged -= HandleAudioStateChanged;

        if (!IsRunning)
        {
            UpdateState(State with
            {
                Status = WidgetServerStatus.Stopped,
                ConnectedClients = 0,
                AudioState = audioService.State
            });
            return;
        }

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        widgetServerHost.Stop();
        _listenerPrefix = null;

        lock (_clients)
        {
            foreach (var client in _clients.ToList())
            {
                try
                {
                    client.Abort();
                }
                catch
                {
                    // ignored
                }
            }

            _clients.Clear();
        }

        UpdateState(State with
        {
            Status = WidgetServerStatus.Stopped,
            ConnectedClients = 0,
            ErrorMessage = null,
            AudioState = audioService.State
        });
    }

    private void UpdateState(WidgetServerState state)
    {
        State = state;
        StateChanged(this, state);
    }

    private static WidgetServerStatus ComputeStatus(bool isRunning, AudioSubsystemState audioState, string? errorMessage)
    {
        if (!string.IsNullOrWhiteSpace(errorMessage))
            return WidgetServerStatus.Error;

        if (!isRunning)
            return WidgetServerStatus.Stopped;

        return audioState.Status == AudioCaptureStatus.Error
            ? WidgetServerStatus.Degraded
            : WidgetServerStatus.Running;
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }

    private bool HasBufferedAudioData()
    {
        lock (_audioLock)
        {
            return _latestAudioData is not null;
        }
    }

    private async Task BroadcastSilenceFrameAsync()
    {
        lock (_audioLock)
        {
            _latestAudioData = null;
        }

        if (!IsRunning)
            return;

        await BroadcastMessageAsync(new byte[SilenceFrameSampleCount * sizeof(float)], WebSocketMessageType.Binary);
    }
}
