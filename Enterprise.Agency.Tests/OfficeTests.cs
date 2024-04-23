namespace Enterprise.Agency.Tests;

[TestClass]
public class OfficeTests
{
    public record Log (string Sender, string Message);

    List<Log> Storage { get; set; } = [];

    [TestMethod]
    public async Task BasicOfficePosting()
    {
        var server = TestFramework.StartServer();

        var n1 = 0;

        var semaphore = new SemaphoreSlim(0, 1);

        var logger = Office<IAgencyContract>.Create(TestFramework.Url)
                        .ReceiveLogs((sender, senderId, message) => Storage.Add(new Log(sender, message)))
                        .Run();

        var office1 = Office<IContractExample1>.Create(TestFramework.Url)
                        .Register<int>(o => o.RequestA, n => { n1 += n; semaphore.Release(); })
                        .Run();

        var office2 = Office<IContractExample2>.Create(TestFramework.Url)
                        .Run();

        office2.Post(o => o.RequestA, 10);

        await semaphore.WaitAsync();

        Assert.AreEqual(10, n1);

        await server.DisposeAsync();
    }

    [TestMethod]
    public async Task OfficePostingWithResponse()
    {
        var server = TestFramework.StartServer();

        var text = "";

        var semaphore = new SemaphoreSlim(0, 1);

        var logger = Office<IAgencyContract>.Create(TestFramework.Url)
                        .ReceiveLogs((sender, senderId, message) => Storage.Add(new Log(sender, message)))
                        .Run();

        var office1 = Office<IContractExample1>.Create(TestFramework.Url)
                        .Register(o => o.RequestText, async () => "ok")
                        .Run();

        var office2 = Office<IContractExample2>.Create(TestFramework.Url)
                        .Run();

        await logger.EstablishConnectionAsync();
        await office1.EstablishConnectionAsync();
        await office2.EstablishConnectionAsync();

        await office2.ConnectToAsync(office1.Me);
        await office2.ConnectToAsync(logger.Me);

        office2.PostWithResponse<string>(o => o.RequestText, s => { text = s; semaphore.Release(); });

        await semaphore.WaitAsync();

        Assert.AreEqual("ok", text);

        await server.DisposeAsync();
    }
}


