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

    public async Task<DataChanged> ImportRequest(string fileName, Model model)
    {
        if (fileName is null) return new DataChanged(false, null);

        var dirInfo = new DirectoryInfo(".");
        var files = dirInfo.GetFiles("*.*", SearchOption.AllDirectories);
        var file = files.FirstOrDefault(x => x.FullName.Contains(fileName));

        model.Update(DataOperator.Import(file));

        model.Process();

        return new DataChanged(true, model.GetPrintable());
    }
}


