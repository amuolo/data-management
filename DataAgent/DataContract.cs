using DataDomain;

namespace DataAgent;

public interface IDataContract
{
    /* in */
    Task ReadRequest();

    Task ImportRequest(string fileName, Model model);

    /* out */
    Task DataChangedEvent();

    Task ShowProgress(double progress);

    Task Write(string message);
}


