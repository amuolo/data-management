using Data.Domain;

namespace Data.Agent;

public interface IDataContract
{
    /* in */
    Task ReadRequest();

    Task ImportRequest(string fileName);

    /* out */
    Task DataChangedEvent();

    Task ShowProgress(double progress);

    Task Display(string message);
}


