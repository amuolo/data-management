namespace Enterprise.Agency.Tests;

[TestClass]
public class OfficeTests
{
    List<TestFramework.Log> Storage { get; set; } = [];

    [TestMethod]
    public async Task BasicPosting()
    {
        var (Server, Logger, Office1, Office2) = await TestFramework.SetupThreeBodyProblemAsync(Storage);

        var n1 = 0;
        var semaphore = new SemaphoreSlim(0, 1);

        Office1.Register<int>(o => o.RequestA, n => { n1 += n; semaphore.Release(); });

        Office2.Post(o => o.RequestA, 10);

        await semaphore.WaitAsync();

        Assert.AreEqual(10, n1);
    }

    [TestMethod]
    public async Task PostingWithResponse()
    {
        var (Server, Logger, Office1, Office2) = await TestFramework.SetupThreeBodyProblemAsync(Storage);

        var text = "";
        var semaphore = new SemaphoreSlim(0, 1);      

        Office1.Register(o => o.RequestText, async () => { await Task.Delay(10); return "ok"; });

        Office2.PostWithResponse<string>(o => o.RequestText, s => { text = s; semaphore.Release(); });

        await semaphore.WaitAsync();

        Assert.AreEqual("ok", text);

        text = "";

        Office2.PostWithResponse<string, string>(Office1.Me, o => o.RequestText, s => { text = s; semaphore.Release(); });

        await semaphore.WaitAsync();

        Assert.AreEqual("ok", text);
    }

    [TestMethod]
    public async Task SynchronousPosting()
    {
        var (Server, Logger, Office1, Office2) = await TestFramework.SetupThreeBodyProblemAsync(Storage);

        var text = "";
        var semaphore = new SemaphoreSlim(0, 1);

        Office1.Register(o => o.RequestTextSync, () => "ok");

        Office2.PostWithResponse<string>(o => o.RequestText, s => { text = s; semaphore.Release(); });

        await semaphore.WaitAsync();

        Assert.AreEqual("ok", text);

        text = "";

        Office2.PostWithResponse<string, string>(Office1.Me, o => o.RequestText, s => { text = s; semaphore.Release(); });

        await semaphore.WaitAsync();

        Assert.AreEqual("ok", text);
    }
}


