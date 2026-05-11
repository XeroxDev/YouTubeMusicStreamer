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

using System.Diagnostics.CodeAnalysis;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using YouTubeMusicStreamer.Extensions;
using YouTubeMusicStreamer.Services.WebSocket;

namespace YouTubeMusicStreamer.Services.App;

public class AudioDeviceInfo(string id, string name)
{
    public string Id { get; } = id;
    public string Name { get; } = name;
}

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class AudioInfo(int sampleRate, int bitsPerSample, int channels, string encoding)
{
    public int SampleRate { get; } = sampleRate;
    public int BitsPerSample { get; } = bitsPerSample;
    public int Channels { get; } = channels;
    public string Encoding { get; } = encoding;
}

public partial class AudioService(IAudioCaptureSessionFactory captureSessionFactory) : IAudioCaptureService, IDisposable
{
    private IAudioCaptureSession? _capture;
    private string? _currentDeviceId;
    private string? _lastError;
    public event EventHandler<byte[]>? DataAvailable;
    public event EventHandler<AudioInfo>? AudioInfoChanged;
    public event EventHandler<AudioSubsystemState>? StateChanged;

    public AudioSubsystemState State { get; private set; } = new(AudioCaptureStatus.Stopped, null, null);

    public AudioInfo? CurrentAudioInfo => _capture?.AudioInfo;

    public Task ApplyConfigurationAsync(bool enabled, string deviceId, CancellationToken cancellationToken = default)
    {
        if (!enabled)
        {
            StopCapture();
            UpdateState(AudioCaptureStatus.Disabled, null, null);
            return Task.CompletedTask;
        }

        if (string.IsNullOrWhiteSpace(deviceId))
        {
            StopCapture();
            UpdateState(AudioCaptureStatus.Error, null, "No audio device is configured.");
            return Task.CompletedTask;
        }

        if (_capture is not null && string.Equals(_currentDeviceId, deviceId, StringComparison.Ordinal))
            return Task.CompletedTask;

        StopCapture();
        StartCapture(deviceId);
        return Task.CompletedTask;
    }

    public void StartCapture(string deviceId)
    {
        if (_capture is not null) return;

        try
        {
            UpdateState(AudioCaptureStatus.Starting, deviceId, null);
            _capture = captureSessionFactory.Create(deviceId);
            _currentDeviceId = deviceId;
            _lastError = null;
            AudioInfoChanged?.Invoke(this, CurrentAudioInfo!);

            _capture.DataAvailable += HandleDataAvailable;
            _capture.RecordingStopped += (_, args) =>
            {
                if (args is not null)
                {
                    _lastError = args.Message;
                    UpdateState(AudioCaptureStatus.Error, _currentDeviceId, _lastError);
                    return;
                }

                if (_capture is null)
                    UpdateState(AudioCaptureStatus.Stopped, _currentDeviceId, null);
            };

            _capture.Start();
            UpdateState(AudioCaptureStatus.Capturing, deviceId, null);
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            UpdateState(AudioCaptureStatus.Error, deviceId, ex.Message);
        }
    }

    public void StopCapture()
    {
        if (_capture is null) return;

        _capture.DataAvailable -= HandleDataAvailable;
        _capture.Stop();
        _capture.Dispose();
        _capture = null;
        UpdateState(AudioCaptureStatus.Stopped, _currentDeviceId, null);
    }

    private void HandleDataAvailable(object? sender, byte[] data)
    {
        var buffer = new byte[data.Length];
        Buffer.BlockCopy(data, 0, buffer, 0, data.Length);
        DataAvailable?.Invoke(this, buffer);
    }

    private void UpdateState(AudioCaptureStatus status, string? deviceId, string? errorMessage)
    {
        State = new AudioSubsystemState(status, deviceId, errorMessage);
        StateChanged?.Invoke(this, State);
    }

    public void Dispose()
    {
        StopCapture();
        GC.SuppressFinalize(this);
    }
}

public interface IAudioDeviceProvider
{
    IReadOnlyList<AudioDeviceInfo> GetDevices(bool forceRefresh = false);
}

public sealed class NAudioDeviceProvider : IAudioDeviceProvider
{
    private List<AudioDeviceInfo> _cachedDevices = [];

    public IReadOnlyList<AudioDeviceInfo> GetDevices(bool forceRefresh = false)
    {
        if (forceRefresh || _cachedDevices.Count == 0)
        {
            _cachedDevices = new MMDeviceEnumerator().EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
                .Select(device => new AudioDeviceInfo(device.ID, device.FriendlyName.CoalesceEmpty(device.DeviceFriendlyName))).ToList();
        }

        return _cachedDevices;
    }
}

public interface IAudioCaptureSessionFactory
{
    IAudioCaptureSession Create(string deviceId);
}

public sealed class WasapiLoopbackCaptureSessionFactory : IAudioCaptureSessionFactory
{
    public IAudioCaptureSession Create(string deviceId)
    {
        var enumerator = new MMDeviceEnumerator();
        var device = enumerator.GetDevice(deviceId);
        return new WasapiLoopbackCaptureSession(new WasapiLoopbackCapture(device));
    }
}

public interface IAudioCaptureSession : IDisposable
{
    event EventHandler<byte[]>? DataAvailable;
    event EventHandler<Exception?>? RecordingStopped;
    AudioInfo AudioInfo { get; }
    void Start();
    void Stop();
}

internal sealed class WasapiLoopbackCaptureSession : IAudioCaptureSession
{
    private readonly WasapiLoopbackCapture _capture;

    public WasapiLoopbackCaptureSession(WasapiLoopbackCapture capture)
    {
        _capture = capture;
        _capture.DataAvailable += HandleDataAvailable;
        _capture.RecordingStopped += HandleRecordingStopped;
    }

    public event EventHandler<byte[]>? DataAvailable;
    public event EventHandler<Exception?>? RecordingStopped;

    public AudioInfo AudioInfo => new(
        _capture.WaveFormat.SampleRate,
        _capture.WaveFormat.BitsPerSample,
        _capture.WaveFormat.Channels,
        _capture.WaveFormat.Encoding.ToString());

    public void Start() => _capture.StartRecording();

    public void Stop() => _capture.StopRecording();

    public void Dispose()
    {
        _capture.DataAvailable -= HandleDataAvailable;
        _capture.RecordingStopped -= HandleRecordingStopped;
        _capture.Dispose();
    }

    private void HandleDataAvailable(object? sender, WaveInEventArgs args)
    {
        var buffer = new byte[args.BytesRecorded];
        Buffer.BlockCopy(args.Buffer, 0, buffer, 0, args.BytesRecorded);
        DataAvailable?.Invoke(this, buffer);
    }

    private void HandleRecordingStopped(object? sender, StoppedEventArgs args)
    {
        RecordingStopped?.Invoke(this, args.Exception);
    }
}

public interface IAudioCaptureService
{
    event EventHandler<byte[]>? DataAvailable;
    event EventHandler<AudioInfo>? AudioInfoChanged;
    event EventHandler<AudioSubsystemState>? StateChanged;
    AudioSubsystemState State { get; }
    AudioInfo? CurrentAudioInfo { get; }
    Task ApplyConfigurationAsync(bool enabled, string deviceId, CancellationToken cancellationToken = default);
    void StopCapture();
}
