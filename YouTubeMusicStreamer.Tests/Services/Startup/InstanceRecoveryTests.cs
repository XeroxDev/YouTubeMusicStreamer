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

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using YouTubeMusicStreamer.Services.Startup;

namespace YouTubeMusicStreamer.Tests.Services.Startup;

public sealed class InstanceRecoveryTests
{
    [Fact]
    public async Task RedirectOrRecoverAsync_ReturnsExit_WhenPrimaryRespondsToShowWindow()
    {
        var environment = new FakeInstanceRecoveryEnvironment
        {
            ShowWindowResult = new InstanceCommandResult(true, "shown")
        };

        var shouldExit = await InstanceRecovery.RedirectOrRecoverAsync(NullLogger.Instance, environment);

        Assert.True(shouldExit);
        Assert.Equal(0, environment.RedirectActivationCallCount);
        Assert.Equal(0, environment.TryAcquirePrimaryOwnershipCallCount);
    }

    [Fact]
    public async Task RedirectOrRecoverAsync_Redirects_WhenStateFileIsMissingOrUnreadable()
    {
        var environment = new FakeInstanceRecoveryEnvironment();

        var shouldExit = await InstanceRecovery.RedirectOrRecoverAsync(NullLogger.Instance, environment);

        Assert.True(shouldExit);
        Assert.Equal(1, environment.RedirectActivationCallCount);
        Assert.Equal(0, environment.TryAcquirePrimaryOwnershipCallCount);
    }

    [Fact]
    public async Task RedirectOrRecoverAsync_RedirectsWithoutKilling_WhenStateFileVersionDoesNotMatch()
    {
        var process = new FakeInstanceRecoveryProcess();
        var environment = new FakeInstanceRecoveryEnvironment
        {
            CurrentAppVersion = "2.0.0",
            Health = CreateHealth(DateTime.UtcNow - TimeSpan.FromMinutes(5), appVersion: "1.0.0"),
            Process = process
        };

        var shouldExit = await InstanceRecovery.RedirectOrRecoverAsync(NullLogger.Instance, environment);

        Assert.True(shouldExit);
        Assert.Equal(1, environment.RedirectActivationCallCount);
        Assert.False(process.KillCalled);
    }

    [Fact]
    public async Task RedirectOrRecoverAsync_RedirectsWithoutKilling_WhenVerifiedProcessIsNotStale()
    {
        var process = new FakeInstanceRecoveryProcess();
        var environment = new FakeInstanceRecoveryEnvironment
        {
            Health = CreateHealth(DateTime.UtcNow),
            Process = process
        };

        var shouldExit = await InstanceRecovery.RedirectOrRecoverAsync(NullLogger.Instance, environment);

        Assert.True(shouldExit);
        Assert.Equal(1, environment.RedirectActivationCallCount);
        Assert.False(process.KillCalled);
        Assert.True(process.Disposed);
    }

    [Fact]
    public async Task RedirectOrRecoverAsync_ReacquiresOwnership_WhenStateReferencesMissingProcess()
    {
        var environment = new FakeInstanceRecoveryEnvironment
        {
            Health = CreateHealth(DateTime.UtcNow - TimeSpan.FromMinutes(5)),
            ProcessExists = false
        };
        environment.EnqueueAcquireResults(false, true);

        var shouldExit = await InstanceRecovery.RedirectOrRecoverAsync(NullLogger.Instance, environment);

        Assert.False(shouldExit);
        Assert.Equal(0, environment.RedirectActivationCallCount);
        Assert.Equal(2, environment.TryAcquirePrimaryOwnershipCallCount);
        Assert.Equal(2, environment.DelayCallCount);
    }

    [Fact]
    public async Task RedirectOrRecoverAsync_KillsStaleVerifiedProcess_AndReacquiresOwnership()
    {
        var process = new FakeInstanceRecoveryProcess();
        var environment = new FakeInstanceRecoveryEnvironment
        {
            Health = CreateHealth(DateTime.UtcNow - TimeSpan.FromMinutes(5)),
            Process = process
        };
        environment.EnqueueAcquireResults(true);

        var shouldExit = await InstanceRecovery.RedirectOrRecoverAsync(NullLogger.Instance, environment);

        Assert.False(shouldExit);
        Assert.True(process.KillCalled);
        Assert.True(process.WaitForExitCalled);
        Assert.True(process.Disposed);
        Assert.Equal(0, environment.RedirectActivationCallCount);
    }

    [Fact]
    public async Task RedirectOrRecoverAsync_Redirects_WhenStaleProcessKillFails()
    {
        var process = new FakeInstanceRecoveryProcess
        {
            KillException = new InvalidOperationException("access denied")
        };
        var environment = new FakeInstanceRecoveryEnvironment
        {
            Health = CreateHealth(DateTime.UtcNow - TimeSpan.FromMinutes(5)),
            Process = process
        };

        var shouldExit = await InstanceRecovery.RedirectOrRecoverAsync(NullLogger.Instance, environment);

        Assert.True(shouldExit);
        Assert.True(process.KillCalled);
        Assert.False(process.WaitForExitCalled);
        Assert.True(process.Disposed);
        Assert.Equal(1, environment.RedirectActivationCallCount);
        Assert.Equal(0, environment.TryAcquirePrimaryOwnershipCallCount);
    }

    [Fact]
    public async Task RedirectOrRecoverAsync_ReturnsExit_WhenOwnershipCannotBeReacquired()
    {
        var environment = new FakeInstanceRecoveryEnvironment
        {
            Health = CreateHealth(DateTime.UtcNow - TimeSpan.FromMinutes(5)),
            ProcessExists = false
        };

        var shouldExit = await InstanceRecovery.RedirectOrRecoverAsync(NullLogger.Instance, environment);

        Assert.True(shouldExit);
        Assert.Equal(10, environment.TryAcquirePrimaryOwnershipCallCount);
        Assert.Equal(10, environment.DelayCallCount);
    }

    private static InstanceHealthState CreateHealth(DateTime lastUpdatedUtc, string appVersion = "1.0.0")
    {
        return new InstanceHealthState(
            1234,
            "launch-token",
            DateTime.UtcNow - TimeSpan.FromMinutes(10),
            lastUpdatedUtc,
            InstanceRuntimeState.Ready,
            StartupPhase.Ready,
            appVersion);
    }

    private sealed class FakeInstanceRecoveryEnvironment : IInstanceRecoveryEnvironment
    {
        private readonly Queue<bool> _acquireResults = [];

        public string CurrentAppVersion { get; set; } = "1.0.0";
        public InstanceCommandResult? ShowWindowResult { get; set; }
        public InstanceHealthState? Health { get; set; }
        public bool ProcessExists { get; set; } = true;
        public FakeInstanceRecoveryProcess Process { get; set; } = new();
        public int RedirectActivationCallCount { get; private set; }
        public int DelayCallCount { get; private set; }
        public int TryAcquirePrimaryOwnershipCallCount { get; private set; }

        public void EnqueueAcquireResults(params bool[] results)
        {
            foreach (var result in results)
                _acquireResults.Enqueue(result);
        }

        public Task<InstanceCommandResult?> TryShowWindowAsync() => Task.FromResult(ShowWindowResult);

        public InstanceHealthState? TryReadStateFile(ILogger logger) => Health;

        public bool TryGetVerifiedProcess(int pid, out IInstanceRecoveryProcess process)
        {
            if (ProcessExists)
            {
                process = Process;
                return true;
            }

            process = null!;
            return false;
        }

        public Task RedirectActivationAsync(ILogger logger)
        {
            RedirectActivationCallCount++;
            return Task.CompletedTask;
        }

        public Task DelayAsync(TimeSpan delay)
        {
            DelayCallCount++;
            return Task.CompletedTask;
        }

        public bool TryAcquirePrimaryOwnership()
        {
            TryAcquirePrimaryOwnershipCallCount++;
            return _acquireResults.Count > 0 && _acquireResults.Dequeue();
        }
    }

    private sealed class FakeInstanceRecoveryProcess : IInstanceRecoveryProcess
    {
        public Exception? KillException { get; set; }
        public bool KillCalled { get; private set; }
        public bool WaitForExitCalled { get; private set; }
        public bool Disposed { get; private set; }

        public void Kill(bool entireProcessTree)
        {
            KillCalled = true;

            if (KillException is not null)
                throw KillException;
        }

        public void WaitForExit(int milliseconds) => WaitForExitCalled = true;

        public void Dispose() => Disposed = true;
    }
}
