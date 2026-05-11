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

using System.Collections.Concurrent;
using YouTubeMusicStreamer.Services.App.Persistence;

namespace YouTubeMusicStreamer.Services.Commands.Cooldowns;

public class CooldownManager : ICooldownManager
{
    private readonly ConcurrentDictionary<string, DateTime> _last = new();
    private long _pruneCounter;

    public bool TryStart(string commandKey, string executorUserId, CommandCooldownScope cooldownScope, uint secs, out int wait)
    {
        wait = 0;
        if (secs <= 0) return true;

        if (Interlocked.Increment(ref _pruneCounter) % 128 == 0)
            PruneExpiredEntries();

        var key = cooldownScope == CommandCooldownScope.PerUser
            ? $"{commandKey}:{executorUserId}"
            : commandKey;

        var prev = _last.GetOrAdd(key, DateTime.MinValue);
        var elapsed = (DateTime.UtcNow - prev).TotalSeconds;
        if (elapsed < secs)
        {
            wait = (int)Math.Ceiling(secs - elapsed);
            return false;
        }

        _last[key] = DateTime.UtcNow;
        return true;
    }

    private void PruneExpiredEntries()
    {
        var cutoff = DateTime.UtcNow.AddHours(-6);
        foreach (var entry in _last)
        {
            if (entry.Value < cutoff)
                _last.TryRemove(entry.Key, out _);
        }
    }
}
