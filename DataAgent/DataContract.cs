using DataDomain;

namespace DataAgent;

public interface IDataContract
{
    Task<DataChanged> ImportRequest(string fileName, Model model);
}

public record DataChanged (bool IsChange, List<string>? Data);

