using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace Agency;

public class Navigator : NavigationManager
{
    public const string Address = "http://localhost:8000/application";

    public const string SignalRAddress = "/signalr-messaging";

    public Navigator(string baseUri, string uri) => Initialize(baseUri, uri);

    public Navigator() { }

    protected override void EnsureInitialized() => Initialize(Address, Address);

    public static HubConnection GetConnection()
        => new HubConnectionBuilder().WithUrl(new Navigator().ToAbsoluteUri(SignalRAddress)).Build();
}

