using Enterprise.MessageHub;
using System.Collections.Concurrent;

namespace Enterprise.Agency.Tests;

[TestClass]
public class ReadRequestTests
{
    readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    ConcurrentBag<Log> Storage { get; set; } = [];

    [TestMethod]
    public async Task BasicTest()
    {
        var (server, logger, project, agentName) = await TestFramework.SetupManagerAgentProjectLogger(Storage);

        var state = "";
        var semaphore = new SemaphoreSlim(0, 1);

        project.PostWithResponse(
            agent => agent.ReadRequest<XModel>,
            (XModel model) => { state = model.Name + model.Surname; semaphore.Release(); });

        var semaphoreState = await semaphore.WaitAsync(Timeout);

        Assert.IsTrue(semaphoreState);
        Assert.AreEqual("PaoloRossi", state);
        Assert.IsTrue(Storage.Any(x => x.Message.Contains(nameof(IHubContract.CreateRequest)) && x.Sender == agentName));
    }

    [TestMethod]
    public async Task BasicTestWithTarget()
    {
        var (server, logger, project, agentName) = await TestFramework.SetupManagerAgentProjectLogger(Storage);

        var state = "";
        var semaphore = new SemaphoreSlim(0, 1);

        project.PostWithResponse(
            new HubAddress(agentName),
            agent => agent.ReadRequest<XModel>,
            (XModel model) => { state = model.Name + model.Surname; semaphore.Release(); });

        var semaphoreState = await semaphore.WaitAsync(Timeout);

        Assert.IsTrue(semaphoreState);
        Assert.AreEqual("PaoloRossi", state);
        Assert.IsTrue(Storage.Any(x => x.Message.Contains(nameof(IHubContract.CreateRequest)) && x.Sender == agentName));
    }
}
