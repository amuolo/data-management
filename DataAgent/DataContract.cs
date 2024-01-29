using DataDomain;

namespace DataAgent;

public interface IDataContract
{
    Task ReadRequest();

    Task ImportRequest(string fileName, Model model);

    Task DataChangedEvent();
}


