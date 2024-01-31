using Agency;
using DataDomain;

namespace DataAgent;

public class DataHub : MessageHub<IDataContract>
{
    public async Task<Model> Create()
    {
        // TODO: finish
        await Task.CompletedTask;
        return new();
    }

    public async Task<List<string>> ReadRequest(Model model)
    {
        return model.GetPrintable();
    }

    public async Task ImportRequest(string fileName, Model model)
    {
        if (fileName is null) return;

        var dirInfo = new DirectoryInfo(".");
        var files = dirInfo.GetFiles("*.*", SearchOption.AllDirectories);
        var file = files.FirstOrDefault(x => x.FullName.Contains(fileName));
        Post(agent => agent.ShowProgress, 1d/3);

        model.Update(DataOperator.Import(file));

        Post(agent => agent.Write, $"File {fileName} imported");
        Post(agent => agent.ShowProgress, 2d/3);

        model.Process();

        Post(agent => agent.Write, $"Data has been processed");
        Post(agent => agent.ShowProgress, 3d/3);
        Post(agent => agent.DataChangedEvent);
    }
}


