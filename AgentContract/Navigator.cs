using Microsoft.AspNetCore.Components;

namespace Agency;

public class Navigator : NavigationManager
{
    public readonly static string Address = "https://localhost:7071/signalr";

    public const string SignalRAddress = "/signalr";

    public Navigator(string baseUri, string uri) => Initialize(baseUri, uri);

    public Navigator() { }

    protected override void EnsureInitialized() => Initialize(Address, Address);
}

