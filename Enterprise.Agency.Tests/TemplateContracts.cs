namespace Enterprise.Agency.Tests;

public interface IContractExample1 : IAgencyContract
{
    /* in */
    Task SomeEvent();

    Task RequestA(int a);

    Task RequestB(int b);

    /* out */
    Task ResponseA(string x);

    Task ResponseB(string y);
}

public interface IContractExample2 : IContractExample1
{
    /* in */
    Task RequestX(int a);

    Task RequestY(int b);

    /* out */
    Task ResponseX(string x);

    Task ResponseY(string y);
}
