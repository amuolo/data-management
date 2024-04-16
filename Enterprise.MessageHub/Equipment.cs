using Enterprise.Utils;
using Microsoft.AspNetCore.SignalR.Client;

namespace Enterprise.MessageHub;

public record Equipment(string Name, SmartStore<Parcel> SmartStore, HubConnection HubConnection)
{
}
