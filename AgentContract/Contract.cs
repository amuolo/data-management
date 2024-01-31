namespace Agency;

public class Contract
{
    public const string SignalRAddress = "/signalR";

    public const string BaseUrl = "https://localhost:7158";

    public const string Url = BaseUrl + SignalRAddress;

    public const string SendMessage = nameof(SendMessage);

    public const string ReceiveMessage = nameof(ReceiveMessage);

    public const string SendResponse = nameof(SendResponse);

    public const string ReceiveResponse = nameof(ReceiveResponse);  

    public const string Create = nameof(Create);

    public const string Delete = nameof(Delete);

    public const string Log = nameof(Log);

    public const string ReceiveLog = nameof(ReceiveLog);
}
