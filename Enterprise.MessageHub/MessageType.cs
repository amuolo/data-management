namespace Enterprise.MessageHub;

public class MessageType
{
    public const string SendMessage = nameof(SendMessage);
    public const string ReceiveMessage = "Receive Message";

    public const string SendResponse = nameof(SendResponse);
    public const string ReceiveResponse = "Receive Response";

    public const string EstablishConnection = nameof(EstablishConnection);
    public const string ConnectionEstablished = "Connection established";

    public const string Log = nameof(Log);
    public const string ReceiveLog = "Receive Log";
}
