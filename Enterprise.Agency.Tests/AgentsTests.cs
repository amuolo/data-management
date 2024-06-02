using Enterprise.MessageHub;
using Enterprise.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Collections.Concurrent;

namespace Enterprise.Agency.Tests;

[TestClass]
public class AgentsTests
{
    readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    [TestMethod]
    public async Task MessageResponse()
    {
        var (server, logger, project, agentName, storage) = await TestFramework.SetupManagerAgentProjectLogger();

        var state = "";
        var semaphore = new SemaphoreSlim(0, 1);

        project.PostWithResponse(
            agent => agent.GetRequest,
            (XModel model) => { state = model.Name + model.Surname; semaphore.Release(); });

        var semaphoreState = await semaphore.WaitAsync(Timeout);

        Assert.IsTrue(semaphoreState, "semaphoreState timeout");
        Assert.AreEqual("PaoloRossi", state);
        Assert.IsTrue(storage.Any(x => x.Message.Contains(nameof(IHubContract.CreateRequest)) && x.Sender == agentName), "CreateRequest not found");
        Assert.IsTrue(!storage.Any(x => x.Message.Contains(nameof(IHubContract.DeleteRequest)) && x.Message.Contains("processing") && x.Sender == agentName), "DeleteRequest processing not found");
        Assert.IsTrue(!storage.Any(x => x.Message.Contains(nameof(IHubContract.DeleteRequest)) && x.Message.Contains(nameof(PostingHub.SendResponse)) && x.Sender == agentName), "DeleteRequest response not found");
        Assert.IsTrue(!storage.Any(x => x.Message.Contains(nameof(IHubContract.DeleteRequest)) && x.Message.Contains(nameof(PostingHub.SendMessage)) && x.Sender == Addresses.Central), "DeleteRequest message not found");
        Assert.IsTrue(storage.Count() < 30, "count mismatch");
    }

    [TestMethod]
    public async Task MessageResponseWithTarget()
    {
        var (server, logger, project, agentName, storage) = await TestFramework.SetupManagerAgentProjectLogger();

        var state = "";
        var semaphore = new SemaphoreSlim(0, 1);

        project.PostWithResponse(
            new HubAddress(agentName),
            agent => agent.GetRequest,
            (XModel model) => { state = model.Name + model.Surname; semaphore.Release(); });

        var semaphoreState = await semaphore.WaitAsync(Timeout);

        Assert.IsTrue(semaphoreState, "semaphoreState timeout");
        Assert.AreEqual("PaoloRossi", state);
        Assert.IsTrue(storage.Any(x => x.Message.Contains(nameof(IHubContract.CreateRequest)) && x.Sender == agentName), "CreateRequest not found");
        Assert.IsTrue(storage.Count() < 30, "count mismatch");
    }

    [TestMethod]
    public async Task AsyncMessageResponse()
    {
        var (server, logger, project, agentName, storage) = await TestFramework.SetupManagerAgentProjectLogger();

        var state = "";
        var semaphore = new SemaphoreSlim(0, 1);

        project.PostWithResponse<XModel>(
            agent => agent.GetRequestAsync,
            model => { state = model.Name + model.Surname; semaphore.Release(); });

        var semaphoreState = await semaphore.WaitAsync(Timeout);

        Assert.IsTrue(semaphoreState, "semaphoreState timeout");
        Assert.AreEqual("PaoloRossi", state);
        Assert.IsTrue(storage.Any(x => x.Message.Contains(nameof(IHubContract.CreateRequest)) && x.Sender == agentName), "CreateRequest not found");
        Assert.IsTrue(storage.Count() < 30, "count mismatch");
    }

    [TestMethod]
    public async Task AsyncMessageResponseWithTarget()
    {
        var (server, logger, project, agentName, storage) = await TestFramework.SetupManagerAgentProjectLogger();

        var state = "";
        var semaphore = new SemaphoreSlim(0, 1);

        project.PostWithResponse(
            new HubAddress(agentName),
            agent => agent.GetRequestAsync,
            (XModel model) => { state = model.Name + model.Surname; semaphore.Release(); });

        var semaphoreState = await semaphore.WaitAsync(Timeout);

        Assert.IsTrue(semaphoreState, "semaphoreState timeout");
        Assert.AreEqual("PaoloRossi", state);
        Assert.IsTrue(storage.Any(x => x.Message.Contains(nameof(IHubContract.CreateRequest)) && x.Sender == agentName), "CreateRequest not found");
        Assert.IsTrue(storage.Count() < 30, "count mismatch");
    }

    [TestMethod]
    public async Task AsyncUpdateWorkflow()
    {
        var (server, logger, project, agentName, storage) = await TestFramework.SetupManagerAgentProjectLogger();

        string state = "", display = "";
        SemaphoreSlim semaphoreState = new(0, 1), semaphoreDisplay = new(0, 1);

        project.Register(o => o.DataChangedEvent, () =>
        {
            project.PostWithResponse(
                agent => agent.GetRequestAsync,
                (XModel model) => { state = model.Name + model.Surname; semaphoreState.Release(); });
        });

        project.Register<string>(o => o.Display, msg => { display = msg; semaphoreDisplay.Release(); });

        project.Post(agent => agent.UpdateRequestAsync, "Marco");

        var sp1 = await semaphoreState.WaitAsync(Timeout);
        var sp2 = await semaphoreDisplay.WaitAsync(Timeout);

        Assert.IsTrue(sp1, "semaphoreState timeout");
        Assert.IsTrue(sp2, "semaphoreDisplay timeout");
        Assert.AreEqual("MarcoRossi", state);
        Assert.AreEqual("Data has been processed", display);
        Assert.IsTrue(storage.Any(x => x.Message.Contains(nameof(IHubContract.CreateRequest)) && x.Sender == agentName), "CreateRequest not found");
        Assert.IsTrue(storage.Count() < 40, "count mismatch");
    }

    [TestMethod]
    public async Task AsyncUpdateWorkflowWithTarget()
    {
        var (server, logger, project, agentName, storage) = await TestFramework.SetupManagerAgentProjectLogger();

        string state = "", display = "";
        SemaphoreSlim semaphoreState = new(0, 1), semaphoreDisplay = new(0, 1);

        project.Register(o => o.DataChangedEvent, () =>
        {
            project.PostWithResponse<XModel>(
                agent => agent.GetRequestAsync,
                model => { state = model.Name + model.Surname; semaphoreState.Release(); });
        });

        project.Register<string>(o => o.Display, msg => { display = msg; semaphoreDisplay.Release(); });

        project.Post(new HubAddress(agentName), agent => agent.UpdateRequestAsync, "Marco");

        var sp1 = await semaphoreState.WaitAsync(Timeout);
        var sp2 = await semaphoreDisplay.WaitAsync(Timeout);

        Assert.IsTrue(sp1, "semaphoreState timeout");
        Assert.IsTrue(sp2, "semaphoreDisplay timeout");
        Assert.AreEqual("MarcoRossi", state);
        Assert.AreEqual("Data has been processed", display);
        Assert.IsTrue(storage.Any(x => x.Message.Contains(nameof(IHubContract.CreateRequest)) && x.Sender == agentName), "CreateRequest not found");
        Assert.IsTrue(storage.Count() < 40, "count mismatch");
    }

    [TestMethod]
    public async Task UpdateWorkflow()
    {
        var (server, logger, project, agentName, storage) = await TestFramework.SetupManagerAgentProjectLogger();

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

        Assert.IsTrue(sp1, "semaphoreState timeout");
        Assert.IsTrue(sp2, "semaphoreDisplay timeout");
        Assert.AreEqual("MarcoRossi", state);
        Assert.AreEqual("Data has been processed", display);
        Assert.IsTrue(storage.Any(x => x.Message.Contains(nameof(IHubContract.CreateRequest)) && x.Sender == agentName), "Create Request not found");
        Assert.IsTrue(storage.Any(x => x.Message.Contains(nameof(IContractAgentX.Display)) && x.Sender == agentName), "Display not found");
        Assert.IsTrue(storage.Any(x => x.Message.Contains(nameof(IContractAgentX.DataChangedEvent)) && x.Sender == agentName), "DataChangedEvent not found");
        Assert.IsTrue(storage.Any(x => x.Message.Contains(nameof(IContractAgentX.UpdateRequest)) && x.Sender == project.Me), "UpdateRequest not found");
        Assert.IsTrue(storage.Any(x => x.Message.Contains(nameof(IAgencyContract.AgentsRegistrationRequest)) && x.Sender == Addresses.Central), "AgentsRegistrationRequest not found");
        Assert.IsTrue(storage.Count() < 50, "count mismatch");
    }

    [TestMethod]
    public async Task TwoAgents()
    {
        var storage = new ConcurrentBag<Log>();

        var server = await TestFramework.StartServerWithManagerAsync(
            [typeof(Agent<XModel, XHub, IContractAgentX>), typeof(Agent<YModel, YHub, IContractAgentY>)]);

        var url = server.Urls.First();
        var xName = typeof(XHub).ExtractName();
        var yName = typeof(YHub).ExtractName();

        var logger = Project<IContractAgentX>.Create(url)
                        .ReceiveLogs((sender, senderId, message) => storage.Add(new Log(sender, message)))
                        .Run();

        var project = Project<IContractAgentY>.Create(url)
                        .AddAgent<XModel, XHub, IContractAgentX>()
                        .AddAgent<YModel, YHub, IContractAgentY>()
                        .Run();
        
        await project.ConnectToAsync(logger.Me);

        string state = "";
        SemaphoreSlim semaphoreState = new(0, 1);

        project.PostWithResponse(o => o.ValidateRequestWithDoubleReturn, "2", (double d) =>
        {
            logger.PostWithResponse(o => o.GetRequest, (XModel x) =>
            {
                state = d.ToString() + " " + x.Name + " " + x.Surname;
                semaphoreState.Release();
            });
        });

        var sp = await semaphoreState.WaitAsync(Timeout);

        Assert.IsTrue(sp, "semaphoreState timeout");
        Assert.AreEqual("2.5 Paolo Rossi", state);
        Assert.IsTrue(storage.Any(x => x.Message.Contains(nameof(IHubContract.CreateRequest)) && x.Sender == xName), "CreateRequest not found");
        Assert.IsTrue(storage.Any(x => x.Message.Contains(nameof(IHubContract.CreateRequest)) && x.Sender == yName), "CreateRequest not found");
        Assert.IsTrue(storage.Count() < 50, "count mismatch");
    }

    [TestMethod]
    public async Task TwoAgentsWithTwoManagers()
    {
        var storage = new ConcurrentBag<Log>();

        var server = await TestFramework.StartServerWithManagerAsync([typeof(Agent<XModel, XHub, IContractAgentX>)]);
        var url = server.Urls.First();

        var builder = Host.CreateApplicationBuilder();

        builder.Services.AddSignalR();
        builder.Services.AddAgencyManager(url, o => o.WithAgentTypes([typeof(Agent<YModel, YHub, IContractAgentY>)]));

        var app = builder.Build();
        app.RunAsync();

        var xName = typeof(XHub).ExtractName();
        var yName = typeof(YHub).ExtractName();

        var logger = Project<IContractAgentX>.Create(url)
                        .ReceiveLogs((sender, senderId, message) => storage.Add(new Log(sender, message)))
                        .AddAgent<XModel, XHub, IContractAgentX>()
                        .Run();

        var project = Project<IContractAgentY>.Create(url)
                        .AddAgent<YModel, YHub, IContractAgentY>()
                        .Run();

        await project.ConnectToAsync(logger.Me);

        string state = "";
        SemaphoreSlim semaphoreState = new(0, 1);

        project.PostWithResponse(o => o.ValidateRequestWithDoubleReturn, "2", (double d) =>
        {
            logger.PostWithResponse(o => o.GetRequest, (XModel x) =>
            {
                state = d.ToString() + " " + x.Name + " " + x.Surname;
                semaphoreState.Release();
            });
        });
        
        var sp = await semaphoreState.WaitAsync(Timeout);

        Assert.IsTrue(sp, "semaphoreState timeout");
        Assert.AreEqual("2.5 Paolo Rossi", state);
        Assert.IsTrue(storage.Any(x => x.Message.Contains(nameof(IHubContract.CreateRequest)) && x.Sender == xName), "CreateRequest not found");
        Assert.IsTrue(storage.Any(x => x.Message.Contains(nameof(IHubContract.CreateRequest)) && x.Sender == yName), "CreateRequest not found");
        Assert.IsTrue(storage.Count() < 70, "count mismatch");
    }
}


