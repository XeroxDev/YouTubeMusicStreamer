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

using System.Net;
using System.Net.WebSockets;

namespace YouTubeMusicStreamer.Services.WebSocket;

public interface IWidgetServerHost
{
    bool IsListening { get; }
    void Start(string prefix);
    void Stop();
    Task<IWidgetServerRequest> AcceptAsync(CancellationToken cancellationToken);
}

public interface IWidgetServerRequest
{
    bool IsWebSocketRequest { get; }
    Task<IWidgetClientConnection> AcceptWebSocketAsync(CancellationToken cancellationToken);
    void RejectBadRequest();
}

public interface IWidgetClientConnection
{
    WebSocketState State { get; }
    Task SendAsync(byte[] data, WebSocketMessageType messageType, CancellationToken cancellationToken);
    Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken);
    Task CloseAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken);
    void Abort();
}

public sealed class HttpListenerWidgetServerHost : IWidgetServerHost, IDisposable
{
    private readonly HttpListener _listener = new();

    public bool IsListening => _listener.IsListening;

    public void Start(string prefix)
    {
        if (!_listener.Prefixes.Contains(prefix))
            _listener.Prefixes.Add(prefix);

        _listener.Start();
    }

    public void Stop() => _listener.Stop();

    public async Task<IWidgetServerRequest> AcceptAsync(CancellationToken cancellationToken)
    {
        var context = await _listener.GetContextAsync();
        cancellationToken.ThrowIfCancellationRequested();
        return new HttpListenerWidgetServerRequest(context);
    }

    public void Dispose() => _listener.Close();
}

public sealed class HttpListenerWidgetServerRequest : IWidgetServerRequest
{
    private readonly HttpListenerContext _context;

    public HttpListenerWidgetServerRequest(HttpListenerContext context)
    {
        _context = context;
    }

    public bool IsWebSocketRequest => _context.Request.IsWebSocketRequest;

    public async Task<IWidgetClientConnection> AcceptWebSocketAsync(CancellationToken cancellationToken)
    {
        var socketContext = await _context.AcceptWebSocketAsync(null);
        cancellationToken.ThrowIfCancellationRequested();
        return new WebSocketWidgetClientConnection(socketContext.WebSocket);
    }

    public void RejectBadRequest()
    {
        _context.Response.StatusCode = 400;
        _context.Response.Close();
    }
}

public sealed class WebSocketWidgetClientConnection : IWidgetClientConnection
{
    private readonly System.Net.WebSockets.WebSocket _socket;

    public WebSocketWidgetClientConnection(System.Net.WebSockets.WebSocket socket)
    {
        _socket = socket;
    }

    public WebSocketState State => _socket.State;

    public Task SendAsync(byte[] data, WebSocketMessageType messageType, CancellationToken cancellationToken) =>
        _socket.SendAsync(new ArraySegment<byte>(data), messageType, true, cancellationToken);

    public Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken) =>
        _socket.ReceiveAsync(buffer, cancellationToken);

    public Task CloseAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken) =>
        _socket.CloseAsync(closeStatus, statusDescription, cancellationToken);

    public void Abort() => _socket.Abort();
}
