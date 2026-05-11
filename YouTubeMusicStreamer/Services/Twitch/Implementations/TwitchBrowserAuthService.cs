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
using System.Text;
using System.Web;
using Microsoft.Maui.ApplicationModel;
using YouTubeMusicStreamer.Services.Twitch.Interfaces;

namespace YouTubeMusicStreamer.Services.Twitch.Implementations;

public sealed class TwitchBrowserAuthService(
    ITwitchAuthBrowserLauncher browserLauncher,
    ITwitchAuthCallbackListenerFactory callbackListenerFactory,
    Func<string>? stateFactory = null) : ITwitchAuthService
{
    private const int CallbackPort = 46783;
    private static readonly TimeSpan AuthTimeout = TimeSpan.FromMinutes(5);
    private const string CallbackPath = "/oauth2redirect";
    private const string CallbackCompletePath = "/oauth2redirect/complete";
    private static readonly Uri RedirectUri = new($"http://localhost:{CallbackPort}/oauth2redirect");
    private static readonly string[] BroadcasterScopes =
    [
        "user:read:chat",
        "chat:read",
        "chat:edit",
        "bits:read",
        "channel:read:subscriptions",
        "channel:read:redemptions",
        "channel:manage:redemptions"
    ];
    private static readonly string[] BotScopes =
    [
        "user:read:chat",
        "chat:read",
        "chat:edit"
    ];

    public async Task<TwitchAuthResult> StartAsync(TwitchAccountRole role, CancellationToken cancellationToken = default)
    {
        using var listener = callbackListenerFactory.Create(CallbackPort);

        try
        {
            listener.Start();
        }
        catch (Exception ex)
        {
            return new TwitchAuthResult(
                TwitchAuthStatus.Failed,
                null,
                $"Failed to bind Twitch auth callback listener on port {CallbackPort}: {ex.Message}");
        }

        var state = (stateFactory ?? (() => Guid.NewGuid().ToString("N")))();
        var authUrl = BuildAuthorizationUri(role, state);

        try
        {
            await browserLauncher.OpenAsync(authUrl);
        }
        catch (Exception ex)
        {
            return new TwitchAuthResult(
                TwitchAuthStatus.Failed,
                null,
                $"Failed to open the system browser for Twitch authentication: {ex.Message}");
        }

        using var timeoutCancellation = new CancellationTokenSource(AuthTimeout);
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCancellation.Token);
        using var registration = linkedCancellation.Token.Register(listener.Stop);

        while (true)
        {
            ITwitchAuthRequestContext context;
            try
            {
                context = await listener.GetContextAsync(linkedCancellation.Token);
            }
            catch (OperationCanceledException) when (linkedCancellation.Token.IsCancellationRequested)
            {
                var message = cancellationToken.IsCancellationRequested
                    ? "Twitch authentication was cancelled."
                    : "Twitch authentication timed out before the browser flow completed.";
                return new TwitchAuthResult(TwitchAuthStatus.Cancelled, null, message);
            }
            catch (HttpListenerException) when (linkedCancellation.Token.IsCancellationRequested)
            {
                var message = cancellationToken.IsCancellationRequested
                    ? "Twitch authentication was cancelled."
                    : "Twitch authentication timed out before the browser flow completed.";
                return new TwitchAuthResult(TwitchAuthStatus.Cancelled, null, message);
            }
            catch (ObjectDisposedException) when (linkedCancellation.Token.IsCancellationRequested)
            {
                var message = cancellationToken.IsCancellationRequested
                    ? "Twitch authentication was cancelled."
                    : "Twitch authentication timed out before the browser flow completed.";
                return new TwitchAuthResult(TwitchAuthStatus.Cancelled, null, message);
            }

            var result = await HandleRequestAsync(context, state);
            if (result is not null)
                return result;
        }
    }

    private static Uri BuildAuthorizationUri(TwitchAccountRole role, string state)
    {
        var scopes = role == TwitchAccountRole.Bot ? BotScopes : BroadcasterScopes;
        var query = HttpUtility.ParseQueryString(string.Empty);
        query["client_id"] = GeneratedBuildInfo.TwitchClientId;
        query["redirect_uri"] = RedirectUri.ToString();
        query["response_type"] = "token";
        query["scope"] = string.Join(" ", scopes);
        query["force_verify"] = "true";
        query["state"] = state;

        var builder = new UriBuilder("https://id.twitch.tv/oauth2/authorize")
        {
            Query = query.ToString() ?? string.Empty
        };

        return builder.Uri;
    }

    private static async Task<TwitchAuthResult?> HandleRequestAsync(ITwitchAuthRequestContext context, string expectedState)
    {
        if (context.HttpMethod == "GET" && IsPath(context.Path, CallbackPath))
        {
            await context.WriteHtmlAsync(CallbackPageHtml);
            return null;
        }

        if (context.HttpMethod == "POST" && IsPath(context.Path, CallbackCompletePath))
        {
            var body = await context.ReadBodyAsync();
            var form = HttpUtility.ParseQueryString(body);
            var fragment = form["fragment"];
            var values = HttpUtility.ParseQueryString((fragment ?? string.Empty).TrimStart('#'));

            var state = values["state"];
            if (!string.Equals(state, expectedState, StringComparison.Ordinal))
            {
                await context.WritePlainTextAsync(HttpStatusCode.BadRequest, "Invalid Twitch auth state.");
                return new TwitchAuthResult(TwitchAuthStatus.Failed, null, "Invalid Twitch authentication state returned.");
            }

            var error = values["error_description"] ?? values["error"];
            if (!string.IsNullOrWhiteSpace(error))
            {
                var status = error.Contains("cancel", StringComparison.OrdinalIgnoreCase)
                    ? TwitchAuthStatus.Cancelled
                    : TwitchAuthStatus.Failed;
                await context.WritePlainTextAsync(HttpStatusCode.OK, "Twitch authentication did not complete successfully. You can close this window.");
                return new TwitchAuthResult(status, null, error);
            }

            var accessToken = values["access_token"];
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                await context.WritePlainTextAsync(HttpStatusCode.BadRequest, "Missing Twitch access token.");
                return new TwitchAuthResult(TwitchAuthStatus.Failed, null, "Twitch authentication completed without an access token.");
            }

            await context.WritePlainTextAsync(HttpStatusCode.OK, "Twitch authentication completed. You can close this window.");
            return new TwitchAuthResult(TwitchAuthStatus.Success, accessToken, null);
        }

        await context.WritePlainTextAsync(HttpStatusCode.NotFound, "Not found.");
        return null;
    }

    private static bool IsPath(string? actualPath, string expectedPath)
    {
        if (string.IsNullOrWhiteSpace(actualPath))
            return false;

        return string.Equals(actualPath.TrimEnd('/'), expectedPath, StringComparison.OrdinalIgnoreCase);
    }

    private const string CallbackPageHtml = """
        <!doctype html>
        <html lang="en">
        <head>
          <meta charset="utf-8" />
          <title>Twitch Authentication</title>
          <style>
            body { font-family: sans-serif; margin: 2rem; background: #0e0e10; color: #efeff1; }
          </style>
        </head>
        <body>
          <p>Completing Twitch authentication...</p>
          <script>
            (async function () {
              const fragment = window.location.hash || "";
              const response = await fetch("/oauth2redirect/complete/", {
                method: "POST",
                headers: { "Content-Type": "application/x-www-form-urlencoded" },
                body: "fragment=" + encodeURIComponent(fragment)
              });
              const text = await response.text();
              document.body.innerHTML = "<p>" + text + "</p>";
            })();
          </script>
        </body>
        </html>
        """;
}

public sealed class MauiTwitchAuthBrowserLauncher : ITwitchAuthBrowserLauncher
{
    public Task OpenAsync(Uri uri) => Launcher.Default.OpenAsync(uri);
}

public sealed class HttpListenerTwitchAuthCallbackListenerFactory : ITwitchAuthCallbackListenerFactory
{
    public ITwitchAuthCallbackListener Create(int port) => new HttpListenerTwitchAuthCallbackListener(port);
}

internal sealed class HttpListenerTwitchAuthCallbackListener : ITwitchAuthCallbackListener
{
    private readonly HttpListener _listener = new();

    public HttpListenerTwitchAuthCallbackListener(int port)
    {
        _listener.Prefixes.Add($"http://localhost:{port}/");
    }

    public void Start() => _listener.Start();

    public void Stop()
    {
        try
        {
            _listener.Stop();
        }
        catch
        {
            // ignored
        }
    }

    public async Task<ITwitchAuthRequestContext> GetContextAsync(CancellationToken cancellationToken)
    {
        var getContextTask = _listener.GetContextAsync();
        var cancellationTask = cancellationToken.AsTask();
        var completed = await Task.WhenAny(getContextTask, cancellationTask);
        if (completed != getContextTask)
            cancellationToken.ThrowIfCancellationRequested();

        return new HttpListenerTwitchAuthRequestContext(await getContextTask);
    }

    public void Dispose() => _listener.Close();
}

internal sealed class HttpListenerTwitchAuthRequestContext(HttpListenerContext context) : ITwitchAuthRequestContext
{
    public string HttpMethod => context.Request.HttpMethod;
    public string? Path => context.Request.Url?.AbsolutePath;

    public Task<string> ReadBodyAsync() =>
        new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEndAsync();

    public async Task WriteHtmlAsync(string html)
    {
        var bytes = Encoding.UTF8.GetBytes(html);
        context.Response.StatusCode = (int)HttpStatusCode.OK;
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes);
        context.Response.Close();
    }

    public async Task WritePlainTextAsync(HttpStatusCode statusCode, string message)
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "text/plain; charset=utf-8";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes);
        context.Response.Close();
    }
}

internal static class CancellationTokenTaskExtensions
{
    public static Task AsTask(this CancellationToken cancellationToken)
    {
        if (!cancellationToken.CanBeCanceled)
            return Task.Delay(Timeout.Infinite, cancellationToken);

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        cancellationToken.Register(static state => ((TaskCompletionSource)state!).TrySetResult(), tcs);
        return tcs.Task;
    }
}
