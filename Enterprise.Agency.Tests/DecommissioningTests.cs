using Enterprise.MessageHub;
using System.Collections.Concurrent;

namespace Enterprise.Agency.Tests;

[TestClass]
public class DecommissioningTests
{
    readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    [TestMethod]
    public async Task AgentDecommissioning()
    {
        var (server, logger, project, agentName, storage) = await TestFramework.SetupManagerAgentProjectLogger(agentsPeriod: 1);

        var state = "";
        var semaphore = new SemaphoreSlim(0, 1);

        project.PostWithResponse(
            agent => agent.GetRequest,
            (XModel model) => { state = model.Name + model.Surname; semaphore.Release(); });

        var semaphoreState = await semaphore.WaitAsync(Timeout);

        Assert.IsTrue(semaphoreState);
        Assert.AreEqual("PaoloRossi", state);

        await Task.Delay(4000);

        Assert.IsTrue(storage.Count() < 40);
        Assert.IsTrue(storage.Any(x => x.Message.Contains(nameof(IHubContract.CreateRequest)) && x.Sender == agentName));
        Assert.IsTrue(storage.Any(x => x.Message.Contains(nameof(IHubContract.DeleteRequest)) && x.Message.Contains("processing") && x.Sender == agentName));
        Assert.IsTrue(storage.Any(x => x.Message.Contains(nameof(IHubContract.DeleteRequest)) && x.Message.Contains(nameof(PostingHub.SendResponse)) && x.Sender == agentName));
        Assert.IsTrue(storage.Any(x => x.Message.Contains(nameof(IHubContract.DeleteRequest)) && x.Message.Contains(nameof(PostingHub.SendMessage)) && x.Sender == Addresses.Central));
    }
}
