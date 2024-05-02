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
        var (server, logger, office, agentName) = await TestFramework.SetupLoggerOfficeAgentAsync(Storage);

        var state = "";
        var semaphore = new SemaphoreSlim(0, 1);

        office.PostWithResponse<XModel>(
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
        var (server, logger, office, agentName) = await TestFramework.SetupLoggerOfficeAgentAsync(Storage);

        var state = "";
        var semaphore = new SemaphoreSlim(0, 1);

        office.PostWithResponse<string, XModel>(
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
        var (server, logger, office, agentName) = await TestFramework.SetupLoggerOfficeAgentAsync(Storage);

        var state = "";
        var semaphore = new SemaphoreSlim(0, 1);

        office.PostWithResponse<XModel>(
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
        var (server, logger, office, agentName) = await TestFramework.SetupLoggerOfficeAgentAsync(Storage);

        var state = "";
        var semaphore = new SemaphoreSlim(0, 1);

        office.PostWithResponse<string, XModel>(
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
        var (server, logger, office, agentName) = await TestFramework.SetupLoggerOfficeAgentAsync(Storage);

        string state = "", display = "";
        SemaphoreSlim semaphoreState = new(0, 1), semaphoreDisplay = new(0, 1);

        office.Register(o => o.DataChangedEvent, () =>
        {
            office.PostWithResponse<XModel>(
                agent => agent.GetRequestAsync,
                model => { state = model.Name + model.Surname; semaphoreState.Release(); });
        });

        office.Register<string>(o => o.Display, msg => { display = msg; semaphoreDisplay.Release(); });

        office.Post(agent => agent.UpdateRequestAsync, "Marco");

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
        var (server, logger, office, agentName) = await TestFramework.SetupLoggerOfficeAgentAsync(Storage);

        string state = "", display = "";
        SemaphoreSlim semaphoreState = new(0, 1), semaphoreDisplay = new(0, 1);

        office.Register(o => o.DataChangedEvent, () =>
        {
            office.PostWithResponse<XModel>(
                agent => agent.GetRequestAsync,
                model => { state = model.Name + model.Surname; semaphoreState.Release(); });
        });

        office.Register<string>(o => o.Display, msg => { display = msg; semaphoreDisplay.Release(); });

        office.Post(agentName, agent => agent.UpdateRequestAsync, "Marco");

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
        var (server, logger, office, agentName) = await TestFramework.SetupLoggerOfficeAgentAsync(Storage);

        string state = "", display = "";
        SemaphoreSlim semaphoreState = new(0, 1), semaphoreDisplay = new(0, 1);

        office.Register(o => o.DataChangedEvent, () =>
        {
            office.PostWithResponse<XModel>(
                agent => agent.GetRequest,
                model => { state = model.Name + model.Surname; semaphoreState.Release(); });
        });

        office.Register<string>(o => o.Display, msg => { display = msg; semaphoreDisplay.Release(); });

        office.Post(agent => agent.UpdateRequest, "Marco");

        var sp1 = await semaphoreState.WaitAsync(Timeout);
        var sp2 = await semaphoreDisplay.WaitAsync(Timeout);

        Assert.IsTrue(sp1);
        Assert.IsTrue(sp2);
        Assert.AreEqual("MarcoRossi", state);
        Assert.AreEqual("Data has been processed", display);
        Assert.IsTrue(Storage.Any(x => x.Message.Contains(nameof(IHubContract.CreateRequest)) && x.Sender == agentName));
        Assert.IsTrue(Storage.Any(x => x.Message.Contains(nameof(IContractAgentX.Display)) && x.Sender == agentName));
        Assert.IsTrue(Storage.Any(x => x.Message.Contains(nameof(IContractAgentX.DataChangedEvent)) && x.Sender == agentName));
        Assert.IsTrue(Storage.Any(x => x.Message.Contains(nameof(IContractAgentX.UpdateRequest)) && x.Sender == office.Me));
        Assert.IsTrue(Storage.Any(x => x.Message.Contains(nameof(IAgencyContract.AgentsRegistrationRequest)) && x.Sender == Addresses.Central));
    }
}


