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
    public async Task MessageResponse()
    {
        var (server, logger, project, agentName) = await TestFramework.SetupLoggerProjectAgentAsync(Storage);

        var state = "";
        var semaphore = new SemaphoreSlim(0, 1);

        project.PostWithResponse<XModel>(
            agent => agent.GetRequest,
            model => { state = model.Name + model.Surname; semaphore.Release(); });

        var semaphoreState = await semaphore.WaitAsync(Timeout);

        Assert.IsTrue(semaphoreState);
        Assert.AreEqual("PaoloRossi", state);
        Assert.IsTrue(Storage.Any(x => x.Message.Contains(nameof(IHubContract.CreateRequest)) && x.Sender == agentName));
    }

    [TestMethod]
    public async Task MessageResponseWithTarget()
    {
        var (server, logger, project, agentName) = await TestFramework.SetupLoggerProjectAgentAsync(Storage);

        var state = "";
        var semaphore = new SemaphoreSlim(0, 1);

        project.PostWithResponse<string, XModel>(
            agentName,
            agent => agent.GetRequest,
            model => { state = model.Name + model.Surname; semaphore.Release(); });

        var semaphoreState = await semaphore.WaitAsync(Timeout);

        Assert.IsTrue(semaphoreState);
        Assert.AreEqual("PaoloRossi", state);
        Assert.IsTrue(Storage.Any(x => x.Message.Contains(nameof(IHubContract.CreateRequest)) && x.Sender == agentName));
    }

    [TestMethod]
    public async Task AsyncMessageResponse()
    {
        var (server, logger, project, agentName) = await TestFramework.SetupLoggerProjectAgentAsync(Storage);

        var state = "";
        var semaphore = new SemaphoreSlim(0, 1);

        project.PostWithResponse<XModel>(
            agent => agent.GetRequestAsync, 
            model => { state = model.Name + model.Surname; semaphore.Release(); });

        var semaphoreState = await semaphore.WaitAsync(Timeout);

        Assert.IsTrue(semaphoreState);
        Assert.AreEqual("PaoloRossi", state);
        Assert.IsTrue(Storage.Any(x => x.Message.Contains(nameof(IHubContract.CreateRequest)) && x.Sender == agentName));
    }

    [TestMethod]
    public async Task AsyncMessageResponseWithTarget()
    {
        var (server, logger, project, agentName) = await TestFramework.SetupLoggerProjectAgentAsync(Storage);

        var state = "";
        var semaphore = new SemaphoreSlim(0, 1);

        project.PostWithResponse<string, XModel>(
            agentName, 
            agent => agent.GetRequestAsync, 
            model => { state = model.Name + model.Surname; semaphore.Release(); });

        var semaphoreState = await semaphore.WaitAsync(Timeout);

        Assert.IsTrue(semaphoreState);
        Assert.AreEqual("PaoloRossi", state);
        Assert.IsTrue(Storage.Any(x => x.Message.Contains(nameof(IHubContract.CreateRequest)) && x.Sender == agentName));
    }

    [TestMethod]
    public async Task AsyncUpdateWorkflow()
    {
        var (server, logger, project, agentName) = await TestFramework.SetupLoggerProjectAgentAsync(Storage);

        string state = "", display = "";
        SemaphoreSlim semaphoreState = new(0, 1), semaphoreDisplay = new(0, 1);

        project.Register(o => o.DataChangedEvent, () =>
        {
            project.PostWithResponse<XModel>(
                agent => agent.GetRequestAsync,
                model => { state = model.Name + model.Surname; semaphoreState.Release(); });
        });

        project.Register<string>(o => o.Display, msg => { display = msg; semaphoreDisplay.Release(); });

        project.Post(agent => agent.UpdateRequestAsync, "Marco");

        var sp1 = await semaphoreState.WaitAsync(Timeout);
        var sp2 = await semaphoreDisplay.WaitAsync(Timeout);

        Assert.IsTrue(sp1);
        Assert.IsTrue(sp2);
        Assert.AreEqual("MarcoRossi", state);
        Assert.AreEqual("Data has been processed", display);
        Assert.IsTrue(Storage.Any(x => x.Message.Contains(nameof(IHubContract.CreateRequest)) && x.Sender == agentName));
    }

    [TestMethod]
    public async Task AsyncUpdateWorkflowWithTarget()
    {
        var (server, logger, project, agentName) = await TestFramework.SetupLoggerProjectAgentAsync(Storage);

        string state = "", display = "";
        SemaphoreSlim semaphoreState = new(0, 1), semaphoreDisplay = new(0, 1);

        project.Register(o => o.DataChangedEvent, () =>
        {
            project.PostWithResponse<XModel>(
                agent => agent.GetRequestAsync,
                model => { state = model.Name + model.Surname; semaphoreState.Release(); });
        });

        project.Register<string>(o => o.Display, msg => { display = msg; semaphoreDisplay.Release(); });

        project.Post(agentName, agent => agent.UpdateRequestAsync, "Marco");

        var sp1 = await semaphoreState.WaitAsync(Timeout);
        var sp2 = await semaphoreDisplay.WaitAsync(Timeout);

        Assert.IsTrue(sp1);
        Assert.IsTrue(sp2);
        Assert.AreEqual("MarcoRossi", state);
        Assert.AreEqual("Data has been processed", display);
        Assert.IsTrue(Storage.Any(x => x.Message.Contains(nameof(IHubContract.CreateRequest)) && x.Sender == agentName));
    }

    [TestMethod]
    public async Task UpdateWorkflow()
    {
        var (server, logger, project, agentName) = await TestFramework.SetupLoggerProjectAgentAsync(Storage);

        string state = "", display = "";
        SemaphoreSlim semaphoreState = new(0, 1), semaphoreDisplay = new(0, 1);

        project.Register(o => o.DataChangedEvent, () =>
        {
            project.PostWithResponse<XModel>(
                agent => agent.GetRequest,
                model => { state = model.Name + model.Surname; semaphoreState.Release(); });
        });

        project.Register<string>(o => o.Display, msg => { display = msg; semaphoreDisplay.Release(); });

        project.Post(agent => agent.UpdateRequest, "Marco");

        var sp1 = await semaphoreState.WaitAsync(Timeout);
        var sp2 = await semaphoreDisplay.WaitAsync(Timeout);

        Assert.IsTrue(sp1);
        Assert.IsTrue(sp2);
        Assert.AreEqual("MarcoRossi", state);
        Assert.AreEqual("Data has been processed", display);
        Assert.IsTrue(Storage.Any(x => x.Message.Contains(nameof(IHubContract.CreateRequest)) && x.Sender == agentName));
        Assert.IsTrue(Storage.Any(x => x.Message.Contains(nameof(IContractAgentX.Display)) && x.Sender == agentName));
        Assert.IsTrue(Storage.Any(x => x.Message.Contains(nameof(IContractAgentX.DataChangedEvent)) && x.Sender == agentName));
        Assert.IsTrue(Storage.Any(x => x.Message.Contains(nameof(IContractAgentX.UpdateRequest)) && x.Sender == project.Me));
        Assert.IsTrue(Storage.Any(x => x.Message.Contains(nameof(IAgencyContract.AgentsRegistrationRequest)) && x.Sender == Addresses.Central));
    }
}


