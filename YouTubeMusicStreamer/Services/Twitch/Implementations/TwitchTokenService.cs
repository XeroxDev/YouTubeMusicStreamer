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

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TwitchLib.Api;
using YouTubeMusicStreamer.Services.App;
using YouTubeMusicStreamer.Services.Twitch.Implementations.EventArgs;
using YouTubeMusicStreamer.Services.Twitch.Interfaces;

namespace YouTubeMusicStreamer.Services.Twitch.Implementations;

public sealed class TwitchTokenService(SettingsService settings, ILogger<TwitchTokenService> logger, TwitchAPI api) : ITwitchTokenService, IHostedService
{
    private CancellationTokenSource? _cts;
    private Task? _backgroundTask;

    public event EventHandler<TokenValidatedEventArgs>? TokenValidated;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _backgroundTask = RunAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cts is not null)
        {
            await _cts.CancelAsync();
            if (_backgroundTask is not null)
                await _backgroundTask;
        }
    }

    private async Task RunAsync(CancellationToken stoppingToken)
    {
        var token = settings.GetSensitiveSettings().TwitchAccessToken;
        if (!string.IsNullOrWhiteSpace(token))
            await ValidateAsync(token);

        var timer = new PeriodicTimer(TimeSpan.FromHours(1));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RefreshIfNeededAsync();
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Twitch token refresh loop cancelled");
        }
    }

    public async Task<bool> ValidateAsync(string token)
    {
        logger.LogInformation("Validating access token");
        api.Settings.AccessToken = token;
        try
        {
            var result = await api.Auth.ValidateAccessTokenAsync(token);
            var valid = result != null;
            TokenValidated?.Invoke(this, new TokenValidatedEventArgs(valid, token));
            return valid;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Token validation failed");
            TokenValidated?.Invoke(this, new TokenValidatedEventArgs(false, token));
            return false;
        }
    }

    public async Task RefreshIfNeededAsync()
    {
        var token = settings.GetSensitiveSettings().TwitchAccessToken;
        if (!string.IsNullOrWhiteSpace(token)) await ValidateAsync(token);
    }
}