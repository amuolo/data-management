namespace Enterprise.JobFactory.Tests;

[TestClass]
public class TestsJobMachine
{
    List<string> Storage { get; set; } = [];

    Action<string> Log => Storage.Add;

    public record MyTypeA(int X, int Y);

    public record MyTypeB(double T, double W);

    [TestMethod]
    public async Task SimpleStateManipulations()
    {
        var r = await Job.JobFactory.New(-1)
            .WithOptions(o => o.WithLogs(Log))
            .WithStep($"s1", n0 => 1)
            .WithStep($"s2", n1 => n1 + 10)
            .WithStep($"s3", n2 => n2 + 20)
            .WithStep($"s4", _ => Task.Delay(10))
            .Start();

        Assert.AreEqual(31, r.State);
        Assert.AreEqual(4, Storage.Count);
    }

    [TestMethod]
    public async Task ChangeOfStateFromNull()
    {
        var r = await Job.JobFactory.New()
            .WithOptions(o => o.WithLogs(Log))
            .WithStep($"s0", () => Log("a"))
            .WithStep($"s1", () => 1)
            .WithStep($"s2", n1 => n1 + 2)
            .WithStep($"s3", n2 => n2 + 3)
            .WithStep($"s4", n3 => Log("b"))
            .Start();

        Assert.AreEqual(6, r.State);
        Assert.AreEqual(7, Storage.Count);
    }

    [TestMethod]
    public async Task DifferentChangeOfStates()
    {
        var r = await Job.JobFactory.New()
            .WithOptions(o => o.WithLogs(Log))
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
        Assert.AreEqual(8, Storage.Count);
    }

    [TestMethod]
    public async Task ProgressHandling()
    {
        int nTot = -1, nSteps = 0, nClose = -1;

        var r = await Job.JobFactory.New()
            .WithOptions(o => o.WithLogs(Log).WithProgress(n => nTot = n, () => nSteps++, () => nClose = 1))
            .WithStep("s1", _ => 1)
            .WithStep("s2", _ => 2)
            .WithStep("s3", _ => 3)
            .Start();

        Assert.AreEqual(3, r.State);
        Assert.AreEqual(3, Storage.Count);
        Assert.AreEqual(3, nTot);
        Assert.AreEqual(3, nSteps);
        Assert.AreEqual(1, nClose);

        r = await r.WithOptions(o => o.ClearProgress())
                   .WithStep("p1", n => n + 1)
                   .Start();

        Assert.AreEqual(4, r.State);
        Assert.AreEqual(4, Storage.Count);
        Assert.AreEqual(3, nTot);
        Assert.AreEqual(3, nSteps);
        Assert.AreEqual(1, nClose);
    }

    [TestMethod]
    public async Task ThrowWithLogger()
    {
        var r = await Job.JobFactory.New()
            .WithOptions(o => o.WithLogs(Log))
            .WithStep("s1", _ => { throw new Exception("bla"); return 1; })
            .WithStep("s2", _ => 2)
            .Start();

        Assert.AreEqual(1, Storage.Count);
        Assert.AreEqual("Exception caught when executing 's1': Exception has been thrown by the target of an invocation.: bla", Storage[0]);

        await r.Start();

        Assert.AreEqual(2, Storage.Count);
        Assert.AreEqual(2, r.State);

        r = await r.WithOptions(o => o.ClearLogs())
                   .WithStep("s1", n => n + 1)
                   .Start();

        Assert.AreEqual(3, r.State);
        Assert.AreEqual(2, Storage.Count);

        try
        {
            r = await r.WithStep("s1", _ => { throw new Exception("bla"); return 1; })
                       .WithStep("s2", _ => 2)
                       .Start();
        }
        catch (Exception e)
        {
            Storage.Add(e.Message);
        }

        Assert.AreEqual(3, Storage.Count);
        Assert.AreEqual("bla", Storage[2]);
    }

    [TestMethod]
    public async Task ClearAllOptions()
    {
        int nTot = -1, nSteps = 0, nClose = -1;

        var r = await Job.JobFactory.New()
            .WithOptions(o => o.WithLogs(Log).WithProgress(n => nTot = n, () => nSteps++, () => nClose = 1))
            .WithOptions(o => o.Clear())
            .WithStep("s1", _ => 1)
            .WithStep("s2", _ => 2)
            .WithStep("s3", _ => 3)
            .Start();

        Assert.AreEqual(3, r.State);
        Assert.AreEqual(0, Storage.Count);
        Assert.AreEqual(-1, nTot);
        Assert.AreEqual(0, nSteps);
        Assert.AreEqual(-1, nClose);
    }

    [TestMethod]
    public async Task ObjectManipulation()
    {
        var r = await Job.JobFactory.New()
            .WithStep($"s1", async () => await Task.Delay(5))
            .WithStep($"s2", async () => { await Task.Delay(5); return new MyTypeA(1, 1); })
            .WithStep($"s3", a => a.X)
            .WithStep($"s4", x => new MyTypeA(x, 2))
            .WithStep($"s5", x => (double)x.Y)
            .WithStep($"s6", async y => { await Task.Delay(5); return new MyTypeB(0.19, y); })
            .WithStep($"s7", b => new MyTypeB(b.W, b.T))
            .Start();

        Assert.AreEqual(new MyTypeB(2, 0.19), r.State);
    }
}
