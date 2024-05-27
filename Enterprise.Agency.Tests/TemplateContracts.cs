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

    Task<XModel> GetRequestAsync();

    void UpdateRequest(string name);

    Task UpdateRequestAsync(string name);

    Task SomeWorkAsync();

    Task SomeWorkAsync(string a);

    Task<int> SomeWorkWithResultAsync(int i);

    /* out */
    Task DataChangedEvent();

    Task Display(string message);
}

public interface IContractAgentY : IAgencyContract
{
    /* in */
    double ValidateRequestWithDoubleReturn(string a);

    YModel ValidateRequestWithObjectReturn(string a);
}
