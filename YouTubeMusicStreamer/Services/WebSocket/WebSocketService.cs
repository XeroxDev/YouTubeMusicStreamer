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

using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using YouTubeMusicStreamer.Services.App;

namespace YouTubeMusicStreamer.Services.WebSocket;

public enum SocketEvents
{
    TrackInfo,
    AudioInfo
}

public partial class WebSocketService(SettingsService settingsService, AudioService audioService) : IDisposable
{
    private readonly HttpListener _listener = new();
    private CancellationTokenSource? _cts;
    private readonly List<System.Net.WebSockets.WebSocket> _clients = [];

    public bool IsRunning => _listener.IsListening;

    public async Task StartAsync()
    {
        if (_listener.IsListening)
            return;

        // Prevent multiple additions if restarted
        var prefix = $"http://localhost:{settingsService.GetAppSettings().PublicPort}/";
        if (!_listener.Prefixes.Contains(prefix))
            _listener.Prefixes.Add(prefix);

        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        _listener.Start();

        audioService.DataAvailable += BroadcastAudioData;
        audioService.AudioInfoChanged += BroadcastAudioInfo;

        if (settingsService.GetAppSettings().AllowAudioCapture)
        {
            var device = settingsService.GetAppSettings().AudioCaptureDevice;
            if (!string.IsNullOrWhiteSpace(device))
            {
                audioService.StartCapture(device);
            }
        }

        while (!_cts.IsCancellationRequested)
        {
            HttpListenerContext? context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (HttpListenerException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            if (context.Request.IsWebSocketRequest)
            {
                var wsContext = await context.AcceptWebSocketAsync(null);
                var client = wsContext.WebSocket;

                lock (_clients)
                {
                    _clients.Add(client);
                }

                _ = Task.Run(() => MonitorClientAsync(client));
                _ = Task.Run(() => OnClientConnectMessage(client));
            }
            else
            {
                context.Response.StatusCode = 400;
                context.Response.Close();
            }
        }
    }

    private async Task MonitorClientAsync(System.Net.WebSockets.WebSocket client)
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

    private async Task OnClientConnectMessage(System.Net.WebSockets.WebSocket client)
    {
        if (_cts?.Token is null) return;

        if (audioService.CurrentAudioInfo is { } audioInfo)
            await client.SendAsync(CreateJsonMessage(SocketEvents.AudioInfo, audioInfo), WebSocketMessageType.Text, true, _cts.Token);
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

    private async Task BroadcastMessageAsync(byte[] data, WebSocketMessageType messageType, CancellationToken token = default)
    {
        List<System.Net.WebSockets.WebSocket> clientsCopy;
        lock (_clients)
        {
            clientsCopy = _clients.ToList();
        }

        foreach (var client in clientsCopy.Where(client => client.State == WebSocketState.Open))
        {
            try
            {
                await client.SendAsync(new ArraySegment<byte>(data), messageType, true, token);
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
            await Task.Delay(33);

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

    public void Stop()
    {
        audioService.DataAvailable -= BroadcastAudioData;
        audioService.AudioInfoChanged -= BroadcastAudioInfo;
        audioService.StopCapture();

        if (!IsRunning)
            return;

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        _listener.Stop();

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
    }

    public void Dispose()
    {
        Stop();
        _listener.Close();
        GC.SuppressFinalize(this);
    }
}