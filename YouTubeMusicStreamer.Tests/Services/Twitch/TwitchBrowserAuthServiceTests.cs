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
using YouTubeMusicStreamer.Services.Twitch;
using YouTubeMusicStreamer.Services.Twitch.Implementations;
using YouTubeMusicStreamer.Services.Twitch.Interfaces;

namespace YouTubeMusicStreamer.Tests.Services.Twitch;

public sealed class TwitchBrowserAuthServiceTests
{
    [Fact]
    public async Task StartAsync_ReturnsFailed_WhenListenerBindThrows()
    {
        var listener = new FakeTwitchAuthCallbackListener
        {
            StartException = new InvalidOperationException("bind failed")
        };
        var service = CreateService(listener);

        var result = await service.StartAsync(TwitchAccountRole.Broadcaster);

        Assert.Equal(TwitchAuthStatus.Failed, result.Status);
        Assert.Contains("bind", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StartAsync_ReturnsFailed_WhenBrowserLaunchThrows()
    {
        var browser = new FakeBrowserLauncher
        {
            OpenException = new InvalidOperationException("browser failed")
        };
        var listener = new FakeTwitchAuthCallbackListener();
        var service = CreateService(listener, browser);

        var result = await service.StartAsync(TwitchAccountRole.Bot);

        Assert.Equal(TwitchAuthStatus.Failed, result.Status);
        Assert.Contains("browser", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(browser.LastOpenedUri);
    }

    [Fact]
    public async Task StartAsync_ReturnsCancelled_WhenCancellationTokenIsCanceled()
    {
        var listener = new FakeTwitchAuthCallbackListener();
        var service = CreateService(listener);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await service.StartAsync(TwitchAccountRole.Broadcaster, cts.Token);

        Assert.Equal(TwitchAuthStatus.Cancelled, result.Status);
        Assert.Contains("cancel", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.True(listener.StopCallCount >= 1);
    }

    [Fact]
    public async Task StartAsync_IgnoresInitialHtmlCallback_AndCompletesOnValidTokenPost()
    {
        var listener = new FakeTwitchAuthCallbackListener();
        var getPage = new FakeTwitchAuthRequestContext("GET", "/oauth2redirect");
        var postBack = new FakeTwitchAuthRequestContext("POST", "/oauth2redirect/complete", body: "fragment=%23access_token%3Dtoken-123%26state%3Dexpected-state");
        listener.Contexts.Enqueue(getPage);
        listener.Contexts.Enqueue(postBack);

        var service = CreateService(listener, stateOverride: "expected-state");

        var result = await service.StartAsync(TwitchAccountRole.Broadcaster);

        Assert.Equal(TwitchAuthStatus.Success, result.Status);
        Assert.Equal("token-123", result.AccessToken);
        Assert.Equal("text/html; charset=utf-8", getPage.LastContentType);
        Assert.Equal(HttpStatusCode.OK, getPage.LastStatusCode);
        Assert.Equal(HttpStatusCode.OK, postBack.LastStatusCode);
    }

    [Fact]
    public async Task StartAsync_FailsOnInvalidReturnedState()
    {
        var listener = new FakeTwitchAuthCallbackListener();
        var postBack = new FakeTwitchAuthRequestContext("POST", "/oauth2redirect/complete", body: "fragment=%23access_token%3Dtoken-123%26state%3Dwrong");
        listener.Contexts.Enqueue(postBack);
        var service = CreateService(listener, stateOverride: "expected-state");

        var result = await service.StartAsync(TwitchAccountRole.Broadcaster);

        Assert.Equal(TwitchAuthStatus.Failed, result.Status);
        Assert.Contains("state", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.BadRequest, postBack.LastStatusCode);
    }

    [Fact]
    public async Task StartAsync_MapsOauthErrorContainingCancel_ToCancelled()
    {
        var listener = new FakeTwitchAuthCallbackListener();
        var postBack = new FakeTwitchAuthRequestContext("POST", "/oauth2redirect/complete", body: "fragment=%23error_description%3Duser%2520cancelled%26state%3Dexpected-state");
        listener.Contexts.Enqueue(postBack);
        var service = CreateService(listener, stateOverride: "expected-state");

        var result = await service.StartAsync(TwitchAccountRole.Broadcaster);

        Assert.Equal(TwitchAuthStatus.Cancelled, result.Status);
        Assert.Contains("cancel", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StartAsync_FailsWhenTokenIsMissing()
    {
        var listener = new FakeTwitchAuthCallbackListener();
        var postBack = new FakeTwitchAuthRequestContext("POST", "/oauth2redirect/complete", body: "fragment=%23state%3Dexpected-state");
        listener.Contexts.Enqueue(postBack);
        var service = CreateService(listener, stateOverride: "expected-state");

        var result = await service.StartAsync(TwitchAccountRole.Broadcaster);

        Assert.Equal(TwitchAuthStatus.Failed, result.Status);
        Assert.Contains("without an access token", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.BadRequest, postBack.LastStatusCode);
    }

    [Fact]
    public async Task StartAsync_IgnoresUnknownRoutes_AndKeepsWaiting()
    {
        var listener = new FakeTwitchAuthCallbackListener();
        var unknown = new FakeTwitchAuthRequestContext("GET", "/something-else");
        var postBack = new FakeTwitchAuthRequestContext("POST", "/oauth2redirect/complete", body: "fragment=%23access_token%3Dtoken-456%26state%3Dexpected-state");
        listener.Contexts.Enqueue(unknown);
        listener.Contexts.Enqueue(postBack);
        var service = CreateService(listener, stateOverride: "expected-state");

        var result = await service.StartAsync(TwitchAccountRole.Bot);

        Assert.Equal(TwitchAuthStatus.Success, result.Status);
        Assert.Equal(HttpStatusCode.NotFound, unknown.LastStatusCode);
        Assert.Equal("token-456", result.AccessToken);
    }

    private static TwitchBrowserAuthService CreateService(
        FakeTwitchAuthCallbackListener listener,
        FakeBrowserLauncher? browser = null,
        string? stateOverride = null)
    {
        browser ??= new FakeBrowserLauncher();
        return new TwitchBrowserAuthService(
            browser,
            new FakeTwitchAuthCallbackListenerFactory(listener),
            stateOverride is null ? null : () => stateOverride);
    }

    private sealed class FakeBrowserLauncher : ITwitchAuthBrowserLauncher
    {
        public Exception? OpenException { get; set; }
        public Uri? LastOpenedUri { get; private set; }

        public Task OpenAsync(Uri uri)
        {
            LastOpenedUri = uri;
            if (OpenException is not null)
                throw OpenException;

            return Task.CompletedTask;
        }
    }

    private sealed class FakeTwitchAuthCallbackListenerFactory(FakeTwitchAuthCallbackListener listener) : ITwitchAuthCallbackListenerFactory
    {
        public ITwitchAuthCallbackListener Create(int port)
        {
            listener.Port = port;
            return listener;
        }
    }

    private sealed class FakeTwitchAuthCallbackListener : ITwitchAuthCallbackListener
    {
        public Queue<ITwitchAuthRequestContext> Contexts { get; } = new();
        public Exception? StartException { get; set; }
        public int Port { get; set; }
        public int StopCallCount { get; private set; }

        public void Start()
        {
            if (StartException is not null)
                throw StartException;
        }

        public void Stop() => StopCallCount++;

        public Task<ITwitchAuthRequestContext> GetContextAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Contexts.Count == 0)
                return Task.FromCanceled<ITwitchAuthRequestContext>(cancellationToken);

            return Task.FromResult(Contexts.Dequeue());
        }

        public void Dispose()
        {
        }
    }

    private sealed class FakeTwitchAuthRequestContext(string httpMethod, string path, string body = "") : ITwitchAuthRequestContext
    {
        public string HttpMethod => httpMethod;
        public string? Path => path;
        public HttpStatusCode? LastStatusCode { get; private set; }
        public string? LastContentType { get; private set; }
        public string? LastResponseBody { get; private set; }

        public Task<string> ReadBodyAsync() => Task.FromResult(body);

        public Task WriteHtmlAsync(string html)
        {
            LastStatusCode = HttpStatusCode.OK;
            LastContentType = "text/html; charset=utf-8";
            LastResponseBody = html;
            return Task.CompletedTask;
        }

        public Task WritePlainTextAsync(HttpStatusCode statusCode, string message)
        {
            LastStatusCode = statusCode;
            LastContentType = "text/plain; charset=utf-8";
            LastResponseBody = message;
            return Task.CompletedTask;
        }
    }
}
