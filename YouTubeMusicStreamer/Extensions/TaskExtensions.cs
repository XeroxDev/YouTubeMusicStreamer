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

namespace YouTubeMusicStreamer.Extensions;

public static class TaskExtensions
{
    private static Exception GetTaskException(Task task) =>
        task.Exception?.InnerException
        ?? (Exception?)task.Exception
        ?? new InvalidOperationException("Task faulted without exception details.");

    public static void FireAndForget(this Task task, Action<Exception>? onError = null)
    {
        if (task.IsCompleted)
        {
            if (task.IsFaulted)
            {
                onError?.Invoke(GetTaskException(task));
            }

            return;
        }

        task.ContinueWith(t => onError?.Invoke(GetTaskException(t)),
            TaskContinuationOptions.OnlyOnFaulted);
    }

    public static void FireAndAfter<T>(this Task<T> task, Action<T> after, Action<Exception>? onError = null)
    {
        if (task.IsCompleted)
        {
            if (task.IsFaulted)
            {
                onError?.Invoke(GetTaskException(task));
            }
            else
            {
                after(task.Result);
            }

            return;
        }

        task.ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                onError?.Invoke(GetTaskException(t));
            }
            else
            {
                after(t.Result);
            }
        });
    }
}
