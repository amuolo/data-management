namespace Enterprise.MessageHub;

public class Addresses
{
    public const string SignalR = "/signalR";

    public const string BaseUrl = "https://localhost:7158";

    public const string Url = BaseUrl + SignalR;

    public const string Logger = nameof(Logger);
}
