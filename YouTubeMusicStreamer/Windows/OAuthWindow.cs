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

using System.Web;
using YouTubeMusicStreamer.Enums;
using YouTubeMusicStreamer.Utils;

namespace YouTubeMusicStreamer.Windows;

public partial class OAuthWindow : Window
{
    private const string RedirectUri = "http://localhost/oauth2redirect";
    private readonly string[] _scopes = { "user:read:chat", "chat:read", "chat:edit", "bits:read", "channel:read:subscriptions", "channel:read:redemptions", "channel:manage:redemptions" };
    private readonly Action<(string? error, ConnectorState state, string? accessToken)> _onTokenReceived;
    private bool _oAuthCompleted;

    public OAuthWindow(Action<(string? error, ConnectorState state, string? accessToken)> onTokenReceived)
    {
        _onTokenReceived = onTokenReceived;

        var oauthUrl = $"https://id.twitch.tv/oauth2/authorize?client_id={GeneratedBuildInfo.TwitchClientId}&redirect_uri={RedirectUri}&response_type=token&scope={string.Join(" ", _scopes)}&force_verify=true";

        var webView = new WebView
        {
            Source = oauthUrl
        };

        webView.Navigated += OnNavigated;

        Title = "Twitch OAuth";
        Page = new ContentPage
        {
            Content = webView
        };
        Page.BackgroundColor = Color.FromArgb("#0e0e10");
    }

    private void OnNavigated(object? sender, WebNavigatedEventArgs e)
    {
        if (!e.Url.StartsWith(RedirectUri, StringComparison.OrdinalIgnoreCase)) return;

        // Extract the token from the fragment
        var uri = new Uri(e.Url);
        var (error, state, accessToken) = ExtractTwitchResponse(uri);

        _oAuthCompleted = true;

        _onTokenReceived((error, state, accessToken));
        Application.Current?.CloseWindow(this);
    }

    protected override void OnDestroying()
    {
        base.OnDestroying();

        if (!_oAuthCompleted)
        {
            _onTokenReceived(("User cancelled authentication", ConnectorState.Error, null));
        }
    }

    private static (string? error, ConnectorState state, string? accessToken) ExtractTwitchResponse(Uri? uri)
    {
        if (uri is null)
        {
            return ("Invalid URI: Authentication response is null", ConnectorState.Error, null);
        }

        var query = HttpUtility.ParseQueryString(uri.Fragment.TrimStart('#'));

        if (query["error"] is not null)
        {
            return (query["error_description"], ConnectorState.Error, null);
        }

        if (query["access_token"] is null)
        {
            return ("Invalid response: Access token is null", ConnectorState.Error, null);
        }

        return (null, ConnectorState.LoggedIn, query["access_token"]);
    }
}