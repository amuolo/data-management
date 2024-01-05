using DataDomain;
using Job;
using Microsoft.AspNetCore.SignalR;

namespace DataAgent;

public interface IDataContract
{
    /* in */
    Task Create(Model model);
    Task ImportRequest(string fileName);

    /* out */
    Task DataChanged();
}

public class DataHub : Hub<IDataContract>
{
    private Job<Model> Jobs { get; set; } = JobFactory.New<Model>();

    public async Task HandleCreate(Model model) => await Task.Run(() => Jobs = JobFactory.New(model));

    public async Task HandleImportRequest(string fileName)
    {
        // TODO: finish
        await Task.CompletedTask;

        await Clients.All.DataChanged();
    }
}


