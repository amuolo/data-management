namespace Enterprise.Agency.Tests;

[TestClass]
public class AgentsTests
{
    readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    List<Log> Storage { get; set; } = [];

    [TestMethod]
    public async Task BasicAsyncMessageHandling()
    {
        var server = await TestFramework.StartServerAsync([typeof(Agent<XModel, XHub, IContractAgentX>)]);
        var url = server.Urls.First();

        var logger = Office<IAgencyContract>.Create(url)
                        .ReceiveLogs((sender, senderId, message) => Storage.Add(new Log(sender, message)))
                        .Run();

        var office = Office<IContractAgentX>.Create(url)
                        .AddAgent<XModel, XHub, IContractAgentX>()
                        .Run();

        await office.ConnectToAsync(logger.Me);

        var x = "";
        var semaphore = new SemaphoreSlim(0, 1);

        office.PostWithResponse<XModel>(agent => agent.GetRequestAsync, model => { x = model.Name + model.Surname; semaphore.Release(); });

        var semaphoreState = await semaphore.WaitAsync(Timeout);

        Assert.IsTrue(semaphoreState);
        Assert.AreEqual("Paolo Rossi", x);
    }
}


