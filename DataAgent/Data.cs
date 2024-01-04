using Microsoft.AspNetCore.SignalR;

namespace DataAgent;

public interface IDataContract
{
    /* in */
    Task ImportRequest(string fileName);

    /* out */
    Task DataChanged();
}

public class DataHub : Hub<IDataContract>
{
    /* in */
    
    public async Task HandleImportRequest(string fileName)
    {
        // TODO: finish
        await Task.CompletedTask;
    }

    /* out */

    public async Task DataChanged()
    {
        await Clients.All.DataChanged();
    }
}


