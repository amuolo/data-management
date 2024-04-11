using Enterprise.Utils;
using Microsoft.AspNetCore.SignalR.Client;

namespace Enterprise.MessageHub;

public record ActorInfo(string Name, SmartStore<Parcel> SmartStore, HubConnection HubConnection)
{
}
