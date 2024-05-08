using System.Collections.Concurrent;

namespace Enterprise.Agency.Tests;

[TestClass]
public class OfficeTests
{
    readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    ConcurrentBag<Log> Storage { get; set; } = [];

    [TestMethod]
    public async Task BasicPosting()
    {
        var (Server, Logger, Office1, Office2) = await TestFramework.SetupThreeOfficesAsync(Storage);

        var n1 = 0;
        var semaphore = new SemaphoreSlim(0, 1);

        Office1.Register<int>(o => o.RequestA, n => { n1 += n; semaphore.Release(); });

        Office2.Post(o => o.RequestA, 10);

        await semaphore.WaitAsync();

        Assert.AreEqual(10, n1);
    }

    [TestMethod]
    public async Task PostingWithResponseAsyncRequest()
    {
        var (Server, Logger, Office1, Office2) = await TestFramework.SetupThreeOfficesAsync(Storage);

        var text = "";
        var semaphore = new SemaphoreSlim(0, 1);
        var semaphoreState = false;

        Office1.Register(o => o.RequestText, async () => { await Task.Delay(10); return "ok"; });

        Office2.PostWithResponse<string>(o => o.RequestText, s => { text = s; semaphore.Release(); });

        semaphoreState = await semaphore.WaitAsync(Timeout);

        Assert.IsTrue(semaphoreState);
        Assert.AreEqual("ok", text);
    }

    [TestMethod]
    public async Task PostingToTargetWithResponseAsyncRequest()
    {
        var (Server, Logger, Office1, Office2) = await TestFramework.SetupThreeOfficesAsync(Storage);

        var text = "";
        var semaphore = new SemaphoreSlim(0, 1);
        var semaphoreState = false;

        Office1.Register(o => o.RequestText, async () => { await Task.Delay(10); return "ok"; });

        Office2.PostWithResponse<string, string>(Office1.Me, o => o.RequestText, s => { text = s; semaphore.Release(); });

        semaphoreState = await semaphore.WaitAsync(Timeout);

        Assert.IsTrue(semaphoreState);
        Assert.AreEqual("ok", text);
    }

    [TestMethod]
    public async Task PostingWithResponseSyncRequest()
    {
        var (Server, Logger, Office1, Office2) = await TestFramework.SetupThreeOfficesAsync(Storage);

        var text = "";
        var semaphore = new SemaphoreSlim(0, 1);
        var semaphoreState = false;

        Office1.Register(o => o.RequestTextSync, () => "ok");

        Office2.PostWithResponse<string>(o => o.RequestTextSync, s => { text = s; semaphore.Release(); });

        semaphoreState = await semaphore.WaitAsync(Timeout);

        Assert.IsTrue(semaphoreState);
        Assert.AreEqual("ok", text);
    }

    [TestMethod]
    public async Task PostingToTargetWithResponseSyncRequest()
    {
        var (Server, Logger, Office1, Office2) = await TestFramework.SetupThreeOfficesAsync(Storage);

        var text = "";
        var semaphore = new SemaphoreSlim(0, 1);
        var semaphoreState = false;

        Office1.Register(o => o.RequestTextSync, () => "ok");

        Office2.PostWithResponse<string, string>(Office1.Me, o => o.RequestTextSync, s => { text = s; semaphore.Release(); });

        semaphoreState = await semaphore.WaitAsync(Timeout);

        Assert.IsTrue(semaphoreState);
        Assert.AreEqual("ok", text);
    }
}


