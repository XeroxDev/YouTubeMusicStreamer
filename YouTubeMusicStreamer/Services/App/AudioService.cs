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

public partial class AudioService : IDisposable
{
    private WasapiLoopbackCapture? _capture;
    private static List<AudioDeviceInfo> _cachedDevices = [];
    public event EventHandler<byte[]>? DataAvailable;
    public event EventHandler<AudioInfo>? AudioInfoChanged;

    public AudioInfo? CurrentAudioInfo => _capture is null
        ? null
        : new AudioInfo(_capture.WaveFormat.SampleRate, _capture.WaveFormat.BitsPerSample, _capture.WaveFormat.Channels, _capture.WaveFormat.Encoding.ToString());

    public static List<AudioDeviceInfo> GetDevices(bool forceRefresh = false)
    {
        if (forceRefresh || _cachedDevices.Count == 0)
        {
            _cachedDevices = new MMDeviceEnumerator().EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
                .Select(device => new AudioDeviceInfo(device.ID, device.FriendlyName.CoalesceEmpty(device.DeviceFriendlyName))).ToList();
        }

        return _cachedDevices;
    }

    public void StartCapture(string deviceId)
    {
        if (_capture is not null) return;

        var enumerator = new MMDeviceEnumerator();
        var device = enumerator.GetDevice(deviceId);
        if (device is null) return;

        _capture = new WasapiLoopbackCapture(device);
        AudioInfoChanged?.Invoke(this, CurrentAudioInfo!);

        _capture.DataAvailable += (_, args) =>
        {
            var buffer = new byte[args.BytesRecorded];
            Buffer.BlockCopy(args.Buffer, 0, buffer, 0, args.BytesRecorded);
            DataAvailable?.Invoke(this, buffer);
        };

        _capture.StartRecording();
    }

    public void StopCapture()
    {
        if (_capture is null) return;

        _capture.StopRecording();
        _capture.Dispose();
        _capture = null;
    }

    public void Dispose()
    {
        StopCapture();
        GC.SuppressFinalize(this);
    }
}