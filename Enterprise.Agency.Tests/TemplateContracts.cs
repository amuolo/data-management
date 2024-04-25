namespace Enterprise.Agency.Tests;

public interface IContractExample1 : IAgencyContract
{
    /* in */
    Task<string> RequestText();

    string RequestTextSync();

    Task RequestA(int a);

    Task RequestB(int b);

    /* out */
    Task SomeEvent();
}

public interface IContractExample2 : IContractExample1
{
}

public interface IContractAgentX : IAgencyContract
{
    /* in */
    XModel GetRequest();

    XModel GetRequestAsync();

    void ImportRequest(string fileName);

    Task ImportRequestAsync(string fileName);

    /* out */
    Task DataChangedEvent();

    Task ShowProgress(double progress);

    Task Display(string message);
}
