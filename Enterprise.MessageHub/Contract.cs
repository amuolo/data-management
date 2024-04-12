namespace Enterprise.MessageHub;

public interface IHubContract
{
    Task ConnectRequest(string name);
    
    Task ConnectionEstablished();

    Task ReceiveLog();

    Task LogReceived();

    Task<DeletionProcess> DeleteRequest();

    Task ReceiveMessage();

    Task ReceiveResponse();
}

public record DeletionProcess(bool Status);

