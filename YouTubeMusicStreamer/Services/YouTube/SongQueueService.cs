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

using Microsoft.Extensions.Logging;
using XeroxDev.YTMDesktop.Companion.Models.Output;
using YouTubeMusicStreamer.Services.App;
using YouTubeMusicStreamer.Services.App.Diagnostics;

namespace YouTubeMusicStreamer.Services.YouTube;

public class SongQueueService(
    SettingsService settingsService,
    IYtmPlaybackController playbackController,
    ILogger<SongQueueService> logger,
    IAppDiagnosticsService diagnosticsService)
{
    private static readonly TimeSpan EndOfTrackWindow = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan PendingSwitchTimeout = TimeSpan.FromSeconds(10);

    private string? _endWindowVideoId;
    private string? _pendingTargetVideoId;
    private DateTimeOffset? _pendingTargetStartedAtUtc;

    public async Task YouTubeStateChanged(StateOutput e)
    {
        try
        {
            var queue = settingsService.GetQueueItems();
            var queueSettings = settingsService.GetQueueSettings();
            var currentVideoId = e.Video.Id;

            if (!string.IsNullOrWhiteSpace(_pendingTargetVideoId))
            {
                if (string.Equals(currentVideoId, _pendingTargetVideoId, StringComparison.Ordinal))
                {
                    await ConfirmPendingQueueAdvanceAsync(_pendingTargetVideoId);
                    ResetPendingSwitch();
                    _endWindowVideoId = currentVideoId;
                    return;
                }

                if (_pendingTargetStartedAtUtc is not null &&
                    DateTimeOffset.UtcNow - _pendingTargetStartedAtUtc > PendingSwitchTimeout)
                {
                    logger.Diagnostic(diagnosticsService, AppDiagnosticSubsystem.Commands)
                        .Warning(AppDiagnosticCategory.Connectivity, "Timed out waiting for YTMDesktop to confirm queue switch.")
                        .WithDetail($"Target video: {_pendingTargetVideoId}")
                        .StatusOnly()
                        .Write();
                    ResetPendingSwitch();
                }

                return;
            }

            if (queue.Count == 0 || !queueSettings.QueueActive)
            {
                _endWindowVideoId = null;
                return;
            }

            if (!string.Equals(_endWindowVideoId, currentVideoId, StringComparison.Ordinal))
                _endWindowVideoId = null;

            var currentSongDuration = TimeSpan.FromSeconds(e.Video.DurationSeconds);
            var currentTime = TimeSpan.FromSeconds(e.Player.VideoProgress);

            if (currentSongDuration - currentTime > EndOfTrackWindow)
                return;

            if (string.Equals(_endWindowVideoId, currentVideoId, StringComparison.Ordinal))
                return;

            var nextSong = queue.FirstOrDefault();
            if (nextSong is null)
                return;

            _endWindowVideoId = currentVideoId;
            await playbackController.ChangeVideoAsync(nextSong.Id);
            _pendingTargetVideoId = nextSong.Id;
            _pendingTargetStartedAtUtc = DateTimeOffset.UtcNow;
        }
        catch (Exception ex)
        {
            logger.Diagnostic(diagnosticsService, AppDiagnosticSubsystem.Commands)
                .Error(AppDiagnosticCategory.InternalFault, "An error occurred while switching queued songs.")
                .WithDetail(ex.Message)
                .WithException(ex)
                .StatusOnly()
                .Write();
            ResetPendingSwitch();
        }
    }

    private async Task ConfirmPendingQueueAdvanceAsync(string targetVideoId)
    {
        await settingsService.UpdateQueueItemsAsync(items =>
        {
            var index = items.FindIndex(item => string.Equals(item.Id, targetVideoId, StringComparison.Ordinal));
            if (index >= 0)
                items.RemoveAt(index);
        });
    }

    private void ResetPendingSwitch()
    {
        _pendingTargetVideoId = null;
        _pendingTargetStartedAtUtc = null;
    }
}
