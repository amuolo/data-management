using System.Collections.Concurrent;

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
        var (Server, Logger, Project1, Project2) = await TestFramework.SetupThreeProjectsAsync(Storage);

        var text = "";
        var semaphore = new SemaphoreSlim(0, 1);
        var semaphoreState = false;

        Project1.Register(o => o.RequestText, async () => { await Task.Delay(10); return "ok"; });

        Project2.PostWithResponse<string>(o => o.RequestText, s => { text = s; semaphore.Release(); });

        semaphoreState = await semaphore.WaitAsync(Timeout);

        Assert.IsTrue(semaphoreState);
        Assert.AreEqual("ok", text);
    }

    [TestMethod]
    public async Task PostingToTargetWithResponseAsyncRequest()
    {
        var (Server, Logger, Project1, Project2) = await TestFramework.SetupThreeProjectsAsync(Storage);

        var text = "";
        var semaphore = new SemaphoreSlim(0, 1);
        var semaphoreState = false;

        Project1.Register(o => o.RequestText, async () => { await Task.Delay(10); return "ok"; });

        Project2.PostWithResponse<string, string>(Project1.Me, o => o.RequestText, s => { text = s; semaphore.Release(); });

        semaphoreState = await semaphore.WaitAsync(Timeout);

        Assert.IsTrue(semaphoreState);
        Assert.AreEqual("ok", text);
    }

    [TestMethod]
    public async Task PostingWithResponseSyncRequest()
    {
        var (Server, Logger, Project1, Project2) = await TestFramework.SetupThreeProjectsAsync(Storage);

        var text = "";
        var semaphore = new SemaphoreSlim(0, 1);
        var semaphoreState = false;

        Project1.Register(o => o.RequestTextSync, () => "ok");

        Project2.PostWithResponse<string>(o => o.RequestTextSync, s => { text = s; semaphore.Release(); });

        semaphoreState = await semaphore.WaitAsync(Timeout);

        Assert.IsTrue(semaphoreState);
        Assert.AreEqual("ok", text);
    }

    [TestMethod]
    public async Task PostingToTargetWithResponseSyncRequest()
    {
        var (Server, Logger, Project1, Project2) = await TestFramework.SetupThreeProjectsAsync(Storage);

        var text = "";
        var semaphore = new SemaphoreSlim(0, 1);
        var semaphoreState = false;

        Project1.Register(o => o.RequestTextSync, () => "ok");

        Project2.PostWithResponse<string, string>(Project1.Me, o => o.RequestTextSync, s => { text = s; semaphore.Release(); });

        semaphoreState = await semaphore.WaitAsync(Timeout);

        Assert.IsTrue(semaphoreState);
        Assert.AreEqual("ok", text);
    }
}


