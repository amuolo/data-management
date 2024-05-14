namespace Enterprise.MessageHub;

public interface IHubAddress
{
    string Name { get; }
}

public record HubAddress(string Name) : IHubAddress
{
}

public class Addresses
{
    public const string SignalR = "/signalR";

    public const string Central = nameof(Central);

    public const string Logger = nameof(Logger);
}
