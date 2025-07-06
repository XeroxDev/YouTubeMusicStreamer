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

using System.Reflection;
using YouTubeMusicStreamer.Attributes;
using YouTubeMusicStreamer.Interfaces;

namespace YouTubeMusicStreamer.Services.WebSocket;

public class WebSocketClientService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly List<IWebSocketClient> _availableClients = [];


    public WebSocketClientService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        Initialize();
    }

    private void Initialize()
    {
        var webSocketClientTypes = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => t.GetCustomAttribute<WebSocketClientAttribute>() is not null && typeof(IWebSocketClient).IsAssignableFrom(t));

        foreach (var type in webSocketClientTypes)
        {
            var attribute = type.GetCustomAttribute<WebSocketClientAttribute>();
            if (attribute is null) continue;

            if (ActivatorUtilities.CreateInstance(_serviceProvider, type) is IWebSocketClient client) _availableClients.Add(client);
        }
    }

    public List<IWebSocketClient> GetAvailableClients() => _availableClients;

    public IWebSocketClient? GetClient(string name) => _availableClients.FirstOrDefault(c => c.Name == name);
}