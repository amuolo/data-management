namespace Enterprise.MessageHub;

public interface IHubContract
{  
    Task CreateRequest();

    Task<DeletionProcess> DeleteRequest();
}

public record DeletionProcess(bool Status);

