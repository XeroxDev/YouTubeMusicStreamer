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

using YouTubeMusicStreamer.Services.App;
using YouTubeMusicStreamer.Services.WebSocket;

namespace YouTubeMusicStreamer.Tests.Services.App;

public sealed class AudioServiceTests
{
    [Fact]
    public async Task ApplyConfigurationAsync_DisablesCapture_AndDoesNotCreateSession()
    {
        var factory = new FakeAudioCaptureSessionFactory();
        using var service = new AudioService(factory);

        await service.ApplyConfigurationAsync(enabled: false, "device-1");

        Assert.Equal(AudioCaptureStatus.Disabled, service.State.Status);
        Assert.Null(service.State.DeviceId);
        Assert.Equal(0, factory.CreateCallCount);
    }

    [Fact]
    public async Task ApplyConfigurationAsync_ReportsError_WhenEnabledWithoutDevice()
    {
        var factory = new FakeAudioCaptureSessionFactory();
        using var service = new AudioService(factory);

        await service.ApplyConfigurationAsync(enabled: true, "   ");

        Assert.Equal(AudioCaptureStatus.Error, service.State.Status);
        Assert.Equal("No audio device is configured.", service.State.ErrorMessage);
        Assert.Equal(0, factory.CreateCallCount);
    }

    [Fact]
    public async Task ApplyConfigurationAsync_StartsCapture_AndRaisesAudioInfoAndState()
    {
        var session = new FakeAudioCaptureSession();
        var factory = new FakeAudioCaptureSessionFactory(session);
        using var service = new AudioService(factory);
        var states = new List<AudioSubsystemState>();
        AudioInfo? audioInfo = null;
        service.StateChanged += (_, state) => states.Add(state);
        service.AudioInfoChanged += (_, info) => audioInfo = info;

        await service.ApplyConfigurationAsync(enabled: true, "device-1");

        Assert.Equal(1, factory.CreateCallCount);
        Assert.Equal("device-1", factory.CreatedDeviceIds.Single());
        Assert.True(session.StartCalled);
        Assert.Equal(AudioCaptureStatus.Capturing, service.State.Status);
        Assert.Equal("device-1", service.State.DeviceId);
        Assert.NotNull(audioInfo);
        Assert.Contains(states, state => state.Status == AudioCaptureStatus.Starting);
        Assert.Contains(states, state => state.Status == AudioCaptureStatus.Capturing);
    }

    [Fact]
    public async Task ApplyConfigurationAsync_DoesNotRestart_WhenSameDeviceIsAlreadyCapturing()
    {
        var session = new FakeAudioCaptureSession();
        var factory = new FakeAudioCaptureSessionFactory(session);
        using var service = new AudioService(factory);

        await service.ApplyConfigurationAsync(enabled: true, "device-1");
        await service.ApplyConfigurationAsync(enabled: true, "device-1");

        Assert.Equal(1, factory.CreateCallCount);
        Assert.False(session.StopCalled);
        Assert.False(session.Disposed);
    }

    [Fact]
    public async Task ApplyConfigurationAsync_StopsExistingCapture_WhenDeviceChanges()
    {
        var oldSession = new FakeAudioCaptureSession();
        var newSession = new FakeAudioCaptureSession();
        var factory = new FakeAudioCaptureSessionFactory(oldSession, newSession);
        using var service = new AudioService(factory);

        await service.ApplyConfigurationAsync(enabled: true, "device-1");
        await service.ApplyConfigurationAsync(enabled: true, "device-2");

        Assert.True(oldSession.StopCalled);
        Assert.True(oldSession.Disposed);
        Assert.True(newSession.StartCalled);
        Assert.Equal(AudioCaptureStatus.Capturing, service.State.Status);
        Assert.Equal("device-2", service.State.DeviceId);
    }

    [Fact]
    public async Task DataAvailable_EmitsDefensiveCopy()
    {
        var session = new FakeAudioCaptureSession();
        var factory = new FakeAudioCaptureSessionFactory(session);
        using var service = new AudioService(factory);
        byte[]? received = null;
        service.DataAvailable += (_, data) => received = data;

        await service.ApplyConfigurationAsync(enabled: true, "device-1");
        var source = new byte[] { 1, 2, 3 };
        session.EmitData(source);
        source[0] = 99;

        Assert.NotSame(source, received);
        Assert.Equal([1, 2, 3], received);
    }

    [Fact]
    public async Task RecordingStoppedWithException_TransitionsToError()
    {
        var session = new FakeAudioCaptureSession();
        var factory = new FakeAudioCaptureSessionFactory(session);
        using var service = new AudioService(factory);

        await service.ApplyConfigurationAsync(enabled: true, "device-1");
        session.EmitStopped(new InvalidOperationException("device unplugged"));

        Assert.Equal(AudioCaptureStatus.Error, service.State.Status);
        Assert.Equal("device-1", service.State.DeviceId);
        Assert.Equal("device unplugged", service.State.ErrorMessage);
    }

    [Fact]
    public async Task FactoryFailure_TransitionsToError()
    {
        var factory = new FakeAudioCaptureSessionFactory
        {
            CreateException = new InvalidOperationException("device unavailable")
        };
        using var service = new AudioService(factory);

        await service.ApplyConfigurationAsync(enabled: true, "device-1");

        Assert.Equal(AudioCaptureStatus.Error, service.State.Status);
        Assert.Equal("device-1", service.State.DeviceId);
        Assert.Equal("device unavailable", service.State.ErrorMessage);
    }

    [Fact]
    public async Task Dispose_StopsActiveCapture()
    {
        var session = new FakeAudioCaptureSession();
        var factory = new FakeAudioCaptureSessionFactory(session);
        var service = new AudioService(factory);

        await service.ApplyConfigurationAsync(enabled: true, "device-1");
        service.Dispose();

        Assert.True(session.StopCalled);
        Assert.True(session.Disposed);
        Assert.Equal(AudioCaptureStatus.Stopped, service.State.Status);
    }

    private sealed class FakeAudioCaptureSessionFactory(params FakeAudioCaptureSession[] sessions) : IAudioCaptureSessionFactory
    {
        private readonly Queue<FakeAudioCaptureSession> _sessions = new(sessions);

        public Exception? CreateException { get; set; }
        public int CreateCallCount { get; private set; }
        public List<string> CreatedDeviceIds { get; } = [];

        public IAudioCaptureSession Create(string deviceId)
        {
            CreateCallCount++;
            CreatedDeviceIds.Add(deviceId);

            if (CreateException is not null)
                throw CreateException;

            return _sessions.Count > 0 ? _sessions.Dequeue() : new FakeAudioCaptureSession();
        }
    }

    private sealed class FakeAudioCaptureSession : IAudioCaptureSession
    {
        public event EventHandler<byte[]>? DataAvailable;
        public event EventHandler<Exception?>? RecordingStopped;
        public AudioInfo AudioInfo { get; } = new(48000, 32, 2, "IeeeFloat");
        public bool StartCalled { get; private set; }
        public bool StopCalled { get; private set; }
        public bool Disposed { get; private set; }

        public void Start() => StartCalled = true;

        public void Stop() => StopCalled = true;

        public void EmitData(byte[] data) => DataAvailable?.Invoke(this, data);

        public void EmitStopped(Exception? exception) => RecordingStopped?.Invoke(this, exception);

        public void Dispose() => Disposed = true;
    }
}
