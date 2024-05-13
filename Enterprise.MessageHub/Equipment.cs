using Enterprise.Utils;
using Microsoft.AspNetCore.SignalR.Client;

namespace Enterprise.MessageHub;

public record Equipment(string Name, SmartStore<Parcel> SmartStore, HubConnection HubConnection)
{
    public ServiceDiscovery ServiceDiscovery { get; set; } = new();
}

public record ServiceDiscovery(bool Active = false, Func<Task>? ConnectAsync = null)
{
}
