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

using YouTubeMusicStreamer.Services;
using YouTubeMusicStreamer.Services.App;

namespace YouTubeMusicStreamer.Interfaces;

public interface IWebSocketClient
{
    public string Name { get; init; }
    public string ImagePath { get; init; }
    
    public string Ratio { get; init; }

    public string Build();
}

public abstract class WebSocketClientBase(IServiceProvider services, string name, string imagePath, string ratio = "3:1") : IWebSocketClient
{
    private SettingsService SettingsService => services.GetRequiredService<SettingsService>();

    public string Name { get; init; } = name;
    public string ImagePath { get; init; } = imagePath;
    
    public string Ratio { get; init; } = ratio;

    public abstract string GetHtml();

    public virtual string GetCss() => string.Empty;

    public virtual string GetJs() => string.Empty;

    public virtual string GetOnOpenJs() => string.Empty;

    public virtual string GetOnMessageJs() => string.Empty;

    public virtual string GetOnCloseJs() => string.Empty;

    public virtual string GetOnErrorJs() => string.Empty;

    public string Build() =>
        $$"""
          <!doctype html>
          <html lang="en">
          <head>
              <meta charset="UTF-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>{{Name}}</title>
              <style>
                  {{GetCss()}}
              </style>
          </head>
          <body>
          {{GetHtml()}}

          <script>
              document.addEventListener('DOMContentLoaded', () => {
                  {{GetJs()}}
                  
                  let ws = new WebSocket('ws://localhost:{{SettingsService.GetAppSettings().PublicPort}}/');
                  
                  ws.onclose = (event) => {
                      ws = new WebSocket(event.target.url);
                      ws.onerror = event.target.onerror;
                      ws.onclose = event.target.onclose;
                      ws.onmessage = event.target.onmessage;
                  };
                  
                  {{(string.IsNullOrWhiteSpace(GetOnOpenJs()) ? string.Empty : $"ws.onopen = {GetOnOpenJs()}")}}
                  {{(string.IsNullOrWhiteSpace(GetOnMessageJs()) ? string.Empty : $"ws.onmessage = {GetOnMessageJs()}")}}
                  {{(string.IsNullOrWhiteSpace(GetOnCloseJs()) ? string.Empty : $"ws.onclose = {GetOnCloseJs()}")}}
                  {{(string.IsNullOrWhiteSpace(GetOnErrorJs()) ? string.Empty : $"ws.onerror = {GetOnErrorJs()}")}}
              });
          </script>
          </body>
          </html>
          """;
}