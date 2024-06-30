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
        var (server, logger, project1, project2, storage) = await TestFramework.SetupThreeProjectsAsync();

        var n1 = 0;
        var semaphore = new SemaphoreSlim(0, 1);

        project1.Register(o => o.RequestA, (int n) => { n1 += n; semaphore.Release(); });

        project2.Post(o => o.RequestA, 10);

        await semaphore.WaitAsync();

        Assert.AreEqual(10, n1);
    }

    [TestMethod]
    public async Task PostingWithResponseAsyncRequest()
    {
        var (server, logger, project1, project2, storage) = await TestFramework.SetupThreeProjectsAsync();

        var text = "";
        var semaphore = new SemaphoreSlim(0, 1);
        var semaphoreState = false;

        project1.Register(o => o.RequestText, async () => { await Task.Delay(10); return "ok"; });

        project2.PostWithResponse(o => o.RequestText, (string s) => { text = s; semaphore.Release(); });

        semaphoreState = await semaphore.WaitAsync(Timeout);

        Assert.IsTrue(semaphoreState);
        Assert.AreEqual("ok", text);
    }

    [TestMethod]
    public async Task PostingParcelWithResponseAsyncRequest()
    {
        var (server, logger, project1, project2, storage) = await TestFramework.SetupThreeProjectsAsync();

        var text = "";
        var semaphore = new SemaphoreSlim(0, 1);
        var semaphoreState = false;

        project1.Register(o => o.ProcessParcel, async (int a) => { await Task.Delay(10); return "ok" + a.ToString(); });

        project2.PostWithResponse(o => o.ProcessParcel, 199, (string s) => { text = s; semaphore.Release(); });

        semaphoreState = await semaphore.WaitAsync(Timeout);

        Assert.IsTrue(semaphoreState);
        Assert.AreEqual("ok199", text);
    }

    [TestMethod]
    public async Task PostingToTargetWithResponseAsyncRequest()
    {
        var (server, logger, project1, project2, storage) = await TestFramework.SetupThreeProjectsAsync();

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
        var (server, logger, project1, project2, storage) = await TestFramework.SetupThreeProjectsAsync();

        var text = "";
        var semaphore = new SemaphoreSlim(0, 1);
        var semaphoreState = false;

        project1.Register(o => o.RequestTextSync, () => "ok");

        project2.PostWithResponse(o => o.RequestTextSync, (string s) => { text = s; semaphore.Release(); });

        semaphoreState = await semaphore.WaitAsync(Timeout);

        Assert.IsTrue(semaphoreState);
        Assert.AreEqual("ok", text);
    }

    [TestMethod]
    public async Task PostingParcelWithResponseSyncRequest()
    {
        var (server, logger, project1, project2, storage) = await TestFramework.SetupThreeProjectsAsync();

        var text = "";
        var semaphore = new SemaphoreSlim(0, 1);
        var semaphoreState = false;

        project1.Register(o => o.ProcessParcelBis, (int i) => "ok" + i.ToString());

        project2.PostWithResponse(o => o.ProcessParcelBis, 200, (string s) => { text = s; semaphore.Release(); });

        semaphoreState = await semaphore.WaitAsync(Timeout);

        Assert.IsTrue(semaphoreState);
        Assert.AreEqual("ok200", text);
    }

    [TestMethod]
    public async Task PostingToTargetWithResponseSyncRequest()
    {
        var (server, logger, project1, project2, storage) = await TestFramework.SetupThreeProjectsAsync();

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

        project.PostWithResponse(o => o.RequestTextSync, (string s) => { text = s; semaphore.Release(); });

        semaphoreState = await semaphore.WaitAsync(Timeout);

        Assert.IsTrue(semaphoreState);
        Assert.AreEqual("ok", text);
    }

    [TestMethod]
    public async Task ProjectsUnderIntenseTraffic()
    {
        var (server, logger, project1, project2, storage) = await TestFramework.SetupThreeProjectsAsync();

        TimeSpan biggerTimeout = TimeSpan.FromSeconds(40);

        var number = 1000;
        var semaphores1 = Enumerable.Range(0, number).Select(i => new SemaphoreSlim(0, 1)).ToArray();
        var semaphores2 = Enumerable.Range(0, number).Select(i => new SemaphoreSlim(0, 1)).ToArray();
        var semaphoreStates1 = Enumerable.Range(0, number).Select(i => false).ToArray();
        var semaphoreStates2 = Enumerable.Range(0, number).Select(i => false).ToArray();

        project1.Register(o => o.ProcessParcelBis, (int a) => "ok1-" + a.ToString());

        project2.Register(o => o.ProcessParcel, async (int a) => { await Task.Delay(10); return "ok2-" + a.ToString(); });

        var tasks1 = Enumerable.Range(0, number).AsParallel().Select(i => Task.Run(async () =>
        {
            var text = "";
            var sp = semaphores1[i];
            project2.PostWithResponse(o => o.ProcessParcelBis, i, (string s) => { text = s; sp.Release(); });
            semaphoreStates1[i] = await sp.WaitAsync(biggerTimeout);
            Assert.IsTrue(semaphoreStates1[i], $"series 1 iteration {i} is failing");
            Assert.AreEqual("ok1-" + i.ToString(), text, $"series 1 iteration {i} returns the following text: {text}");
        })).ToArray();
        
        var tasks2 = Enumerable.Range(0, number).AsParallel().Select(i => Task.Run(async () =>
        {
            var text = "";
            var sp = semaphores2[i];
            project1.PostWithResponse(o => o.ProcessParcel, i, (string s) => { text = s; sp.Release(); });
            semaphoreStates2[i] = await sp.WaitAsync(biggerTimeout);
            Assert.IsTrue(semaphoreStates2[i], $"series 2 iteration {i} is failing");
            Assert.AreEqual("ok2-" + i.ToString(), text, $"series 2 iteration {i} returns the following text: {text}");
        })).ToArray();
        
        foreach (var task in tasks1)
            await task;

        foreach (var task in tasks2)
            await task;

        Assert.IsTrue(semaphoreStates1.All(x => x), $"not every state 1 is positive");
        Assert.IsTrue(semaphoreStates2.All(x => x), $"not every state 2 is positive");
    }
}


