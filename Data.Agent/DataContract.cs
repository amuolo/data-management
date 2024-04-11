using Data.Domain;
using Enterprise.Agency;

namespace Data.Agent;

public interface IDataContract : IAgencyContract
{
    /* in */
    Task<List<string>> ReadRequest();

    Task ImportRequest(string fileName);

    /* out */
    Task DataChangedEvent();

    Task ShowProgress(double progress);

    Task Display(string message);
}


