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

using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;
using YouTubeMusicStreamer.Services.App;
using YouTubeMusicStreamer.Services.App.Persistence;
using YouTubeMusicStreamer.Services.WebSocket;
using YouTubeMusicStreamer.Tests.TestSupport;

namespace YouTubeMusicStreamer.Tests.Services.WebSocket;

public sealed class WebSocketServiceTests
{
    [Fact]
    public async Task StartAsync_LoadsSavedSettings_AppliesAudioConfiguration_AndTransitionsRunning()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        await harness.Settings.InitializeAsync();
        await harness.Settings.SaveYouTubeSettingsAsync(new YouTubeSettingsSnapshot("127.0.0.1", 9863, false, 8090, true, "device-1"));

        var audio = new FakeAudioCaptureService();
        var host = new FakeWidgetServerHost();
        using var service = CreateService(harness.Settings, audio, host);

        await service.StartAsync();

        var applyCall = Assert.Single(audio.ApplyCalls);
        Assert.Equal((true, "device-1"), applyCall);
        Assert.Equal("http://localhost:8090/", Assert.Single(host.StartPrefixes));
        Assert.True(service.IsRunning);
        Assert.Equal(WidgetServerStatus.Running, service.State.Status);
        Assert.Equal(8090, service.State.Configuration.Port);
        Assert.True(service.State.Configuration.AudioEnabled);
        Assert.Equal("device-1", service.State.Configuration.AudioDeviceId);
    }

    [Fact]
    public async Task ApplyConfigurationAsync_RestartsHostUsingPassedConfiguration_WhenPortChanges()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        await harness.Settings.InitializeAsync();
        await harness.Settings.SaveYouTubeSettingsAsync(new YouTubeSettingsSnapshot("127.0.0.1", 9863, false, 8090, false, string.Empty));

        var audio = new FakeAudioCaptureService();
        var host = new FakeWidgetServerHost();
        using var service = CreateService(harness.Settings, audio, host);
        await service.StartAsync();

        await service.ApplyConfigurationAsync(new YouTubeSettingsSnapshot("127.0.0.1", 9863, false, 9001, true, "device-9"));

        Assert.Equal(["http://localhost:8090/", "http://localhost:9001/"], host.StartPrefixes);
        Assert.Equal(1, host.StopCallCount);
        Assert.Equal(9001, service.State.Configuration.Port);
        Assert.True(service.State.Configuration.AudioEnabled);
        Assert.Equal("device-9", service.State.Configuration.AudioDeviceId);
    }

    [Fact]
    public async Task WebSocketClientConnection_ReceivesCachedAudioAndTrackInfo_OnConnect()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        await harness.Settings.InitializeAsync();

        var audio = new FakeAudioCaptureService
        {
            CurrentAudioInfo = new AudioInfo(48000, 32, 2, "IeeeFloat")
        };
        var host = new FakeWidgetServerHost();
        var client = new FakeWidgetClientConnection();
        using var service = CreateService(harness.Settings, audio, host);

        await service.StartAsync();
        await service.BroadcastTrackInfoAsync(new { title = "Song A" });
        host.Enqueue(new FakeWidgetServerRequest(client));

        await WaitForConditionAsync(() => client.SentMessages.Count >= 2);

        Assert.Equal(1, service.State.ConnectedClients);
        Assert.Contains(client.SentMessages, message => message.MessageType == WebSocketMessageType.Text && Encoding.UTF8.GetString(message.Data).Contains("\"e\":\"AudioInfo\""));
        Assert.Contains(client.SentMessages, message => message.MessageType == WebSocketMessageType.Text && Encoding.UTF8.GetString(message.Data).Contains("\"e\":\"TrackInfo\""));
    }

    [Fact]
    public async Task NonWebSocketRequest_IsRejected_WithoutAddingClient()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        await harness.Settings.InitializeAsync();

        var host = new FakeWidgetServerHost();
        var request = new FakeWidgetServerRequest(isWebSocketRequest: false);
        using var service = CreateService(harness.Settings, new FakeAudioCaptureService(), host);

        await service.StartAsync();
        host.Enqueue(request);

        await WaitForConditionAsync(() => request.Rejected);

        Assert.True(request.Rejected);
        Assert.Equal(0, service.State.ConnectedClients);
    }

    [Fact]
    public async Task AudioStateChange_BroadcastsSilenceFrame_WhenCaptureStopsWithBufferedAudio()
    {
        await using var harness = SettingsPersistenceHarness.Create();
        await harness.Settings.InitializeAsync();

        var audio = new FakeAudioCaptureService();
        var host = new FakeWidgetServerHost();
        var client = new FakeWidgetClientConnection();
        using var service = CreateService(harness.Settings, audio, host);

        await service.StartAsync();
        host.Enqueue(new FakeWidgetServerRequest(client));
        await WaitForConditionAsync(() => service.State.ConnectedClients == 1);

        audio.EmitStateChanged(new AudioSubsystemState(AudioCaptureStatus.Capturing, "device-1", null));
        audio.EmitDataAvailable([1, 2, 3, 4]);
        audio.EmitStateChanged(new AudioSubsystemState(AudioCaptureStatus.Stopped, "device-1", null));

        await WaitForConditionAsync(() => client.SentMessages.Any(message =>
            message.MessageType == WebSocketMessageType.Binary && message.Data.Length == 4096));

        Assert.Contains(client.SentMessages, message => message.MessageType == WebSocketMessageType.Binary && message.Data.Length == 4096);
    }

    private static WebSocketService CreateService(
        SettingsService settings,
        FakeAudioCaptureService audio,
        FakeWidgetServerHost host)
    {
        return new WebSocketService(
            settings,
            audio,
            host,
            ImmediateDelay.Instance);
    }

    private static async Task WaitForConditionAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (condition())
                return;

            await Task.Delay(10);
        }

        throw new TimeoutException("Condition was not met within the expected test window.");
    }

    private sealed class FakeAudioCaptureService : IAudioCaptureService
    {
        public event EventHandler<byte[]>? DataAvailable;
        public event EventHandler<AudioInfo>? AudioInfoChanged;
        public event EventHandler<AudioSubsystemState>? StateChanged;

        public List<(bool Enabled, string DeviceId)> ApplyCalls { get; } = [];
        public AudioSubsystemState State { get; private set; } = new(AudioCaptureStatus.Stopped, null, null);
        public AudioInfo? CurrentAudioInfo { get; set; }
        public int StopCaptureCallCount { get; private set; }

        public Task ApplyConfigurationAsync(bool enabled, string deviceId, CancellationToken cancellationToken = default)
        {
            ApplyCalls.Add((enabled, deviceId));
            return Task.CompletedTask;
        }

        public void StopCapture() => StopCaptureCallCount++;

        public void EmitDataAvailable(byte[] data) => DataAvailable?.Invoke(this, data);
        public void EmitAudioInfo(AudioInfo info)
        {
            CurrentAudioInfo = info;
            AudioInfoChanged?.Invoke(this, info);
        }

        public void EmitStateChanged(AudioSubsystemState state)
        {
            State = state;
            StateChanged?.Invoke(this, state);
        }
    }

    private sealed class FakeWidgetServerHost : IWidgetServerHost
    {
        private readonly Channel<IWidgetServerRequest> _requests = Channel.CreateUnbounded<IWidgetServerRequest>();

        public bool IsListening { get; private set; }
        public List<string> StartPrefixes { get; } = [];
        public int StopCallCount { get; private set; }

        public void Start(string prefix)
        {
            IsListening = true;
            StartPrefixes.Add(prefix);
        }

        public void Stop()
        {
            IsListening = false;
            StopCallCount++;
        }

        public async Task<IWidgetServerRequest> AcceptAsync(CancellationToken cancellationToken) =>
            await _requests.Reader.ReadAsync(cancellationToken);

        public void Enqueue(IWidgetServerRequest request) => _requests.Writer.TryWrite(request);
    }

    private sealed class FakeWidgetServerRequest : IWidgetServerRequest
    {
        public FakeWidgetServerRequest(FakeWidgetClientConnection? client = null, bool isWebSocketRequest = true)
        {
            Client = client;
            IsWebSocketRequest = isWebSocketRequest;
        }

        public FakeWidgetClientConnection? Client { get; }
        public bool IsWebSocketRequest { get; }
        public bool Rejected { get; private set; }

        public Task<IWidgetClientConnection> AcceptWebSocketAsync(CancellationToken cancellationToken)
        {
            if (Client is null)
                throw new InvalidOperationException("No fake client was configured.");

            return Task.FromResult<IWidgetClientConnection>(Client);
        }

        public void RejectBadRequest() => Rejected = true;
    }

    private sealed class FakeWidgetClientConnection : IWidgetClientConnection
    {
        private readonly TaskCompletionSource<WebSocketReceiveResult> _receiveTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public WebSocketState State { get; private set; } = WebSocketState.Open;
        public List<(byte[] Data, WebSocketMessageType MessageType)> SentMessages { get; } = [];

        public Task SendAsync(byte[] data, WebSocketMessageType messageType, CancellationToken cancellationToken)
        {
            SentMessages.Add((data.ToArray(), messageType));
            return Task.CompletedTask;
        }

        public async Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken) =>
            await _receiveTcs.Task.WaitAsync(cancellationToken);

        public Task CloseAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
        {
            State = WebSocketState.Closed;
            _receiveTcs.TrySetResult(new WebSocketReceiveResult(0, WebSocketMessageType.Close, true));
            return Task.CompletedTask;
        }

        public void Abort()
        {
            State = WebSocketState.Aborted;
            _receiveTcs.TrySetResult(new WebSocketReceiveResult(0, WebSocketMessageType.Close, true));
        }
    }

    private sealed class ImmediateDelay : IAsyncDelay
    {
        public static ImmediateDelay Instance { get; } = new();

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
