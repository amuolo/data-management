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
