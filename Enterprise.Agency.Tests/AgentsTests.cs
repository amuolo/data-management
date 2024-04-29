using Enterprise.MessageHub;
using Enterprise.Utils;
using System.Collections.Concurrent;

namespace Enterprise.Agency.Tests;

[TestClass]
public class AgentsTests
{
    readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    ConcurrentBag<Log> Storage { get; set; } = [];

    [TestMethod]
    public async Task BasicAsyncMessageHandling()
    {
        var (server, logger, office, agentName) = await TestFramework.SetupLoggerOfficeAgentAsync(Storage);

        var x = "";
        var semaphore = new SemaphoreSlim(0, 1);

        office.PostWithResponse<XModel>(
            agent => agent.GetRequestAsync, 
            model => { x = model.Name + model.Surname; semaphore.Release(); });

        var semaphoreState = await semaphore.WaitAsync(Timeout);

        Assert.IsTrue(semaphoreState);
        Assert.AreEqual("PaoloRossi", x);

        Assert.IsTrue(Storage.Any(x => x.Message.Contains(nameof(IHubContract.CreateRequest)) && x.Sender == agentName));
    }

    [TestMethod]
    public async Task BasicAsyncMessageHandlingWithTarget()
    {
        var (server, logger, office, agentName) = await TestFramework.SetupLoggerOfficeAgentAsync(Storage);

        var x = "";
        var semaphore = new SemaphoreSlim(0, 1);

        office.PostWithResponse<string, XModel>(
            agentName, 
            agent => agent.GetRequestAsync, 
            model => { x = model.Name + model.Surname; semaphore.Release(); });

        var semaphoreState = await semaphore.WaitAsync(Timeout);

        Assert.IsTrue(semaphoreState);
        Assert.AreEqual("PaoloRossi", x);

        Assert.IsTrue(Storage.Any(x => x.Message.Contains(nameof(IHubContract.CreateRequest)) && x.Sender == agentName));
    }
}


