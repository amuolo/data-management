namespace Enterprise.MessageHub;

public interface IHubContract
{  
    Task ReceiveLog();

    Task LogReceived();

    Task CreateRequest();

    Task ReadRequest();

    Task ReadResponse();

    Task<DeletionProcess> DeleteRequest();

    Task ReceiveMessage();

    Task ReceiveResponse();
}

public record DeletionProcess(bool Status);

