namespace Enterprise.Agency.Tests;

[TestClass]
public class OfficeTesting
{
    List<string> Storage { get; set; } = [];

    Action<string> Log => Storage.Add;

    [TestMethod]
    public async Task BasicOfficePosting()
    {
        var server = TestFrameworkSetup.StartServer();

        var n1 = 0;

        var semaphore = new SemaphoreSlim(0, 1);

        var office1 = Office<IContractExample1>.Create(TestFrameworkSetup.Url)
                        .Register<int>(o => o.RequestA, n => { n1 += n; semaphore.Release(); })
                        .Run();

        var office2 = Office<IContractExample2>.Create(TestFrameworkSetup.Url)
                        .Run();

        office2.Post(o => o.RequestA, 10);

        await semaphore.WaitAsync();

        Assert.AreEqual(10, n1);

        await server.DisposeAsync();
    }

    [TestMethod]
    public async Task OfficePostingWithResponse()
    {
        var server = TestFrameworkSetup.StartServer();

        await server.DisposeAsync();
    }
}


