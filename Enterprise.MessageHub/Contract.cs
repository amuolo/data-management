namespace Enterprise.MessageHub;

public interface IHubContract
{  
    Task CreateRequest();

    Task ReadRequest();

    Task ReadResponse();

    Task<DeletionProcess> DeleteRequest();
}

public record DeletionProcess(bool Status);

