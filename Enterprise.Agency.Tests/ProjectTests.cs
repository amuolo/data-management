using System.Collections.Concurrent;
using Enterprise.MessageHub;
using Enterprise.Utils;

namespace Enterprise.Agency.Tests;

[TestClass]
public class ProjectTests
{
    readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    ConcurrentBag<Log> Storage { get; set; } = [];

    [TestMethod]
    public async Task BasicPosting()
    {
        var (Server, Logger, Project1, Project2) = await TestFramework.SetupThreeProjectsAsync(Storage);

        var n1 = 0;
        var semaphore = new SemaphoreSlim(0, 1);

        Project1.Register<int>(o => o.RequestA, n => { n1 += n; semaphore.Release(); });

        Project2.Post(o => o.RequestA, 10);

        await semaphore.WaitAsync();

        Assert.AreEqual(10, n1);
    }

    [TestMethod]
    public async Task PostingWithResponseAsyncRequest()
    {
        var (server, logger, project1, project2) = await TestFramework.SetupThreeProjectsAsync(Storage);

        var text = "";
        var semaphore = new SemaphoreSlim(0, 1);
        var semaphoreState = false;

        project1.Register(o => o.RequestText, async () => { await Task.Delay(10); return "ok"; });

        project2.PostWithResponse<string>(o => o.RequestText, s => { text = s; semaphore.Release(); });

        semaphoreState = await semaphore.WaitAsync(Timeout);

        Assert.IsTrue(semaphoreState);
        Assert.AreEqual("ok", text);
    }

    [TestMethod]
    public async Task PostingToTargetWithResponseAsyncRequest()
    {
        var (server, logger, project1, project2) = await TestFramework.SetupThreeProjectsAsync(Storage);

        var text = "";
        var semaphore = new SemaphoreSlim(0, 1);
        var semaphoreState = false;

        project1.Register(o => o.RequestText, async () => { await Task.Delay(10); return "ok"; });

        project2.PostWithResponse(new HubAddress(project1.Me), o => o.RequestText, (string s) => { text = s; semaphore.Release(); });

        semaphoreState = await semaphore.WaitAsync(Timeout);

        Assert.IsTrue(semaphoreState);
        Assert.AreEqual("ok", text);
    }

    [TestMethod]
    public async Task PostingWithResponseSyncRequest()
    {
        var (server, logger, project1, project2) = await TestFramework.SetupThreeProjectsAsync(Storage);

        var text = "";
        var semaphore = new SemaphoreSlim(0, 1);
        var semaphoreState = false;

        project1.Register(o => o.RequestTextSync, () => "ok");

        project2.PostWithResponse<string>(o => o.RequestTextSync, s => { text = s; semaphore.Release(); });

        semaphoreState = await semaphore.WaitAsync(Timeout);

        Assert.IsTrue(semaphoreState);
        Assert.AreEqual("ok", text);
    }

    [TestMethod]
    public async Task PostingToTargetWithResponseSyncRequest()
    {
        var (server, logger, project1, project2) = await TestFramework.SetupThreeProjectsAsync(Storage);

        var text = "";
        var semaphore = new SemaphoreSlim(0, 1);
        var semaphoreState = false;

        project1.Register(o => o.RequestTextSync, () => "ok");

        project2.PostWithResponse(new HubAddress(project1.Me), o => o.RequestTextSync, (string s) => { text = s; semaphore.Release(); });

        semaphoreState = await semaphore.WaitAsync(Timeout);

        Assert.IsTrue(semaphoreState);
        Assert.AreEqual("ok", text);
    }

    [TestMethod]
    public async Task LoggerProjectInPresenceOfSuperfluousManager()
    {
        var server = await TestFramework.StartServerWithManagerAsync([typeof(Agent<XModel, XHub, IContractAgentX>)]);
        var url = server.Urls.First();
        var agentName = typeof(XHub).ExtractName();

        var logger = Project<IContractExample1>.Create(url)
                        .ReceiveLogs((sender, senderId, message) => Storage.Add(new Log(sender, message)))
                        .Run();

        var project = Project<IContractExample2>.Create(url)
                        .AddAgent<XModel, XHub, IContractAgentX>()
                        .Run();

        await project.ConnectToAsync(logger.Me);

        var text = "";
        var semaphore = new SemaphoreSlim(0, 1);
        var semaphoreState = false;

        logger.Register(o => o.RequestTextSync, () => "ok");

        project.PostWithResponse<string>(o => o.RequestTextSync, s => { text = s; semaphore.Release(); });

        semaphoreState = await semaphore.WaitAsync(Timeout);

        Assert.IsTrue(semaphoreState);
        Assert.AreEqual("ok", text);
    }
}


