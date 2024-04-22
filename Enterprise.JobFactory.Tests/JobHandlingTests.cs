using Enterprise.Job;

namespace Tests.JobMachine;

[TestClass]
public class TestsJobMachine
{
    List<string> Logs { get; set; } = [];

    Action<string> Logger => s => Logs.Add(s);

    [TestMethod]
    public async Task SimpleStateManipulations()
    {
        var r = await JobFactory.New(-1)
            .WithOptions(o => o.WithLogs(Logger))
            .WithStep($"s1", n0 => 1)
            .WithStep($"s2", n1 => n1 + 10)
            .WithStep($"s3", n2 => n2 + 20)
            .WithStep($"s4", _ => Task.Delay(10))
            .Start();

        Assert.AreEqual(31, r.State);
        Assert.AreEqual(4, Logs.Count);
    }

    [TestMethod]
    public async Task ChangeOfStateFromNull()
    { 
        var r = await JobFactory.New()
            .WithOptions(o => o.WithLogs(Logger))
            .WithStep($"s0", () => Logger("a"))
            .WithStep($"s1", () => 1)
            .WithStep($"s2", n1 => n1 + 2)
            .WithStep($"s3", n2 => n2 + 3)
            .WithStep($"s4", n3 => Logger("b"))
            .Start();

        Assert.AreEqual(6, r.State);
        Assert.AreEqual(7, Logs.Count);
    }

    [TestMethod]
    public async Task DifferentChangeOfStates()
    {
        var r = await JobFactory.New()
            .WithOptions(o => o.WithLogs(Logger))
            .WithStep($"s1", () => 1)
            .WithStep($"s2", n1 => $"{n1+2}")
            .WithStep($"s3", n2 => int.Parse(n2) + 6.9)
            .WithStep($"s4", async n3 => { await Task.Delay(10); return n3; })
            .WithStep($"s5", async n4 => { await Task.Delay(10); return $"{n4}"; })
            .WithStep($"s6", n5 => "." + n5)
            .WithStep($"s7", _ => Task.Delay(10))
            .WithStep($"s8", async _ => await Task.Delay(10))
            .Start();

        Assert.AreEqual(".9.9", r.State);
        Assert.AreEqual(8, Logs.Count);
    }

    [TestMethod]
    public async Task ProgressHandling()
    {
        int nTot = -1, nSteps = 0, nClose = -1;

        var r = await JobFactory.New()
            .WithOptions(o => o.WithLogs(Logger).WithProgress(n => nTot = n, () => nSteps++, () => nClose = 1))
            .WithStep("s1", _ => 1)
            .WithStep("s2", _ => 2)
            .WithStep("s3", _ => 3)
            .Start();

        Assert.AreEqual(3, r.State);
        Assert.AreEqual(3, Logs.Count);
        Assert.AreEqual(3, nTot);
        Assert.AreEqual(3, nSteps);
        Assert.AreEqual(1, nClose);
    }

    [TestMethod]
    public async Task ThrowWithLogger()
    {
        var r = await JobFactory.New()
            .WithOptions(o => o.WithLogs(Logger))
            .WithStep("s1", _ => { throw new Exception("bla"); return 1; })
            .WithStep("s2", _ => 2)
            .Start();

        Assert.AreEqual(1, Logs.Count);
        Assert.AreEqual("Exception caught when executing 's1': Exception has been thrown by the target of an invocation.: bla", Logs[0]);

        try
        {
            r = await JobFactory.New()
                .WithStep("s1", _ => { throw new Exception("bla"); return 1; })
                .WithStep("s2", _ => 2)
                .Start();
        }
        catch (Exception e)
        {
            Logs.Add(e.Message);
        }

        Assert.AreEqual(2, Logs.Count);
        Assert.AreEqual("Exception has been thrown by the target of an invocation.", Logs[1]);
    }
}
