using Agency;
using DataDomain;
using Microsoft.AspNetCore.SignalR;

namespace DataAgent;

public interface IDataContract
{
    /* in */
    Task ImportRequest(string? fileName);

    /* out */
    Task DataChanged();
}

public class DataHub : Hub<IDataContract>
{
    public static async Task<Model> Create(Model model, IHubContext<DataHub, IDataContract> hub)
    {
        // TODO: finish
        await Task.CompletedTask;
        return new();
    }

    public static async Task HandleImportRequest(string? fileName, Model model, IHubContext<DataHub, IDataContract> hub)
    {
        // TODO: finish
        await Task.CompletedTask;

        await hub.Clients.All.DataChanged();
    }
}


