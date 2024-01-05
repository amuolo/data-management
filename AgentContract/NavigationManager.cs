using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace Agency;

public class Navigator : NavigationManager
{
    public Navigator(string baseUri, string uri) => Initialize(baseUri, uri);

    public Navigator() { }

    protected override void EnsureInitialized()
        => Initialize("http://localhost:8000/application", "http://localhost:8000/application");

    public static HubConnection GetConnection()
        => new HubConnectionBuilder().WithUrl(new Navigator().ToAbsoluteUri("/signalr-messaging")).Build();
}

