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

using Microsoft.Extensions.DependencyInjection;
using System.Runtime.CompilerServices;
using YouTubeMusicStreamer.Interfaces;
using YouTubeMusicStreamer.Services.WebSocket;

namespace YouTubeMusicStreamer.Tests.Services.WebSocket;

public sealed class WebSocketClientServiceTests
{
    [Fact]
    public void Constructor_DiscoversBuiltInClients_InDeterministicNameOrder()
    {
        using var services = CreateServices();
        var service = new WebSocketClientService(services);

        var clients = service.GetAvailableClients();

        Assert.Equal(["Minimal", "Minimal Progressive", "Modern"], clients.Select(x => x.Name));
    }

    [Fact]
    public void GetClient_ReturnsExpectedClient_ByExactName()
    {
        using var services = CreateServices();
        var service = new WebSocketClientService(services);

        var client = service.GetClient("Modern");

        Assert.NotNull(client);
        Assert.Equal("Modern", client!.Name);
        Assert.Equal("/images/clients/modern.gif", client.ImagePath);
    }

    [Fact]
    public void GetClient_ReturnsNull_WhenClientDoesNotExist()
    {
        using var services = CreateServices();
        var service = new WebSocketClientService(services);

        var client = service.GetClient("Does not exist");

        Assert.Null(client);
    }

    [Fact]
    public void GetAvailableClients_ReturnsReadOnlySnapshot_ThatCannotBeMutatedByCallers()
    {
        using var services = CreateServices();
        var service = new WebSocketClientService(services);

        var clients = service.GetAvailableClients();

        Assert.IsAssignableFrom<IReadOnlyList<IWebSocketClient>>(clients);
        Assert.False(clients is List<IWebSocketClient>);
        Assert.Throws<NotSupportedException>(() => ((IList<IWebSocketClient>)clients).Clear());
        Assert.Equal(["Minimal", "Minimal Progressive", "Modern"], service.GetAvailableClients().Select(x => x.Name));
    }

    private static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton(CreateWebSocketServiceStub());
        return services.BuildServiceProvider();
    }

    private static WebSocketService CreateWebSocketServiceStub()
    {
        var service = (WebSocketService)RuntimeHelpers.GetUninitializedObject(typeof(WebSocketService));
        var state = new WidgetServerState(
            new WidgetServerConfiguration(8080, false, string.Empty),
            WidgetServerStatus.Stopped,
            0,
            null,
            new AudioSubsystemState(AudioCaptureStatus.Disabled, null, null),
            null);

        var stateField = typeof(WebSocketService).GetField("<State>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                         ?? throw new InvalidOperationException("WebSocketService State backing field was not found.");
        stateField.SetValue(service, state);
        return service;
    }
}
