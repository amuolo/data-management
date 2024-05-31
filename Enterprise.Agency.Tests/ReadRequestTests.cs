using Enterprise.MessageHub;
using System.Collections.Concurrent;

namespace Enterprise.Agency.Tests;

[TestClass]
public class ReadRequestTests
{
    readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    [TestMethod]
    public async Task BasicTest()
    {
        var (server, logger, project, agentName, storage) = await TestFramework.SetupManagerAgentProjectLogger();

        var state = "";
        var semaphore = new SemaphoreSlim(0, 1);

        project.PostWithResponse(
            agent => agent.ReadRequest<XModel>,
            (XModel model) => { state = model.Name + model.Surname; semaphore.Release(); });

        var semaphoreState = await semaphore.WaitAsync(Timeout);

        Assert.IsTrue(semaphoreState);
        Assert.AreEqual("PaoloRossi", state);
        Assert.IsTrue(storage.Any(x => x.Message.Contains(nameof(IHubContract.CreateRequest)) && x.Sender == agentName));
    }

    [TestMethod]
    public async Task BasicTestWithTarget()
    {
        var (server, logger, project, agentName, storage) = await TestFramework.SetupManagerAgentProjectLogger();

        var state = "";
        var semaphore = new SemaphoreSlim(0, 1);

        project.PostWithResponse(
            new HubAddress(agentName),
            agent => agent.ReadRequest<XModel>,
            (XModel model) => { state = model.Name + model.Surname; semaphore.Release(); });

        var semaphoreState = await semaphore.WaitAsync(Timeout);

        Assert.IsTrue(semaphoreState);
        Assert.AreEqual("PaoloRossi", state);
        Assert.IsTrue(storage.Any(x => x.Message.Contains(nameof(IHubContract.CreateRequest)) && x.Sender == agentName));
    }

    [TestMethod]
    public async Task ParallelReadRequest()
    {
        var (server, logger, project, agentName, storage) = await TestFramework.SetupManagerAgentProjectLogger();

        var state = 2;
        var counter = 0;
        var semaphore = new SemaphoreSlim(0, 1);

        project.PostWithResponse(
            new HubAddress(agentName), 
            agent => agent.SomeWorkWithResultAsync, 
            state, (int i) => { state = i; semaphore.Release(); });

        Enumerable.Range(0, 100).ToList().AsParallel().ForAll(i =>
        {
            project.PostWithResponse(
                new HubAddress(agentName),
                agent => agent.ReadRequest<XModel>,
                (XModel model) => Interlocked.Increment(ref counter));
        });
        
        var semaphoreState = await semaphore.WaitAsync(Timeout);

        Assert.IsTrue(semaphoreState);
        Assert.IsTrue(counter > 50);
        Assert.IsTrue(storage.Any(x => x.Message.Contains(nameof(IHubContract.CreateRequest)) && x.Sender == agentName));
    }
}
