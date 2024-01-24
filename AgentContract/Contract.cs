﻿namespace Agency;

public class Contract
{
    public const string SignalRAddress = "/signalR";

    public const string BaseUrl = "https://localhost:7158";

    public const string Url = BaseUrl + SignalRAddress;

    public const string SendMessage = "SendMessage";

    public const string ReceiveMessage = "ReceiveMessage";

    public const string ReceiveResponse = "ReceiveResponse";

    public const string Create = "Create";

    public const string Log = "Log";

    public const string ReceiveLog = "ReceiveLog";
}
