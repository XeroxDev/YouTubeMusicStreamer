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

using YouTubeMusicStreamer.Extensions;

namespace YouTubeMusicStreamer.Tests.Extensions;

public sealed class TaskExtensionsTests
{
    [Fact]
    public void FireAndForget_WhenTaskIsAlreadyFaulted_InvokesErrorCallbackWithInnerException()
    {
        var error = new InvalidOperationException("boom");
        var observed = default(Exception);

        Task.FromException(error).FireAndForget(ex => observed = ex);

        Assert.Same(error, observed);
    }

    [Fact]
    public async Task FireAndForget_WhenTaskFaultsLater_InvokesErrorCallbackOnce()
    {
        var taskCompletionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbackCompletionSource = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var error = new InvalidOperationException("boom");

        taskCompletionSource.Task.FireAndForget(ex => callbackCompletionSource.TrySetResult(ex));

        taskCompletionSource.SetException(error);

        var observed = await callbackCompletionSource.Task;
        Assert.Same(error, observed);
    }

    [Fact]
    public void FireAndAfter_WhenTaskIsAlreadyCompleted_InvokesAfterCallbackImmediately()
    {
        var observed = 0;

        Task.FromResult(42).FireAndAfter(value => observed = value);

        Assert.Equal(42, observed);
    }

    [Fact]
    public async Task FireAndAfter_WhenTaskCompletesLater_InvokesAfterCallbackAfterCompletion()
    {
        var taskCompletionSource = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbackCompletionSource = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        taskCompletionSource.Task.FireAndAfter(value => callbackCompletionSource.TrySetResult(value));

        taskCompletionSource.SetResult(73);

        var observed = await callbackCompletionSource.Task;
        Assert.Equal(73, observed);
    }

    [Fact]
    public void FireAndAfter_WhenTaskIsAlreadyFaulted_InvokesErrorCallbackWithInnerException()
    {
        var error = new InvalidOperationException("boom");
        var observed = default(Exception);
        var afterCalled = false;

        Task.FromException<int>(error).FireAndAfter(_ => afterCalled = true, ex => observed = ex);

        Assert.Same(error, observed);
        Assert.False(afterCalled);
    }

    [Fact]
    public async Task FireAndAfter_WhenTaskFaultsLater_InvokesErrorCallbackOnce()
    {
        var taskCompletionSource = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbackCompletionSource = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var error = new InvalidOperationException("boom");
        var afterCalled = false;

        taskCompletionSource.Task.FireAndAfter(_ => afterCalled = true, ex => callbackCompletionSource.TrySetResult(ex));

        taskCompletionSource.SetException(error);

        var observed = await callbackCompletionSource.Task;
        Assert.Same(error, observed);
        Assert.False(afterCalled);
    }
}
