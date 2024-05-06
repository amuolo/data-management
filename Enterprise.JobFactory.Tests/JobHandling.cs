namespace Enterprise.JobFactory.Tests;

[TestClass]
public class JobHandling
{
    List<string> Storage { get; set; } = [];

    Action<string> Log => Storage.Add;

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
    public async Task Initialization()
    {
        var r = await Job.JobFactory.New<string>()
            .WithStep($"s1", s => s + 2.ToString())
            .WithStep($"s2", s => s + 3.ToString())
            .Start();

        Assert.AreEqual("23", r.State);
    }

    [TestMethod]
    public async Task InitializationWithValue()
    {
        var r = await Job.JobFactory.New<string>()
            .Initialize("1")
            .WithStep($"s1", s => s + 2.ToString())
            .WithStep($"s2", s => s + 3.ToString())
            .Start();

        Assert.AreEqual("123", r.State);
    }

    [TestMethod]
    public async Task InitializationWithDefault()
    {
        var s = "";
        var r = Job.JobFactory.New<MyTypeA>().WithStep($"s1", a => a with { X = 1 });

        try
        {
            await r.Start();
        }
        catch (Exception ex)
        {
            s = ex.Message;
        }

        Assert.IsTrue(s.Contains("Object reference not set to an instance of an object."));

        await r.WithStep($"s2", a => new MyTypeA(0,0)).WithStep($"s3", a => a with { X=1 }).Start();

        Assert.AreEqual(new MyTypeA(1,0), r.State);
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
    public async Task MutableObjectManipulation()
    {
        var r = await Job.JobFactory.New()
            .WithStep($"s1", async () => { await Task.Delay(5); return new MyTypeC { S1 = "a" }; })
            .WithStep($"s2", c => { c.S1 += "b"; })
            .WithStep($"s3", c => { c.S2 = "c"; })
            .Start();

        Assert.AreEqual("ab", r.State?.S1?? "");
        Assert.AreEqual("c", r.State?.S2?? "");
    }

    [TestMethod]
    public async Task ValueTupleObjectManipulation()
    {
        var r = await Job.JobFactory.New()
            .WithStep($"s1", async () => { await Task.Delay(5); return ("a", "b"); })
            .WithStep($"s2", c => { c.Item1 += "b"; })
            .WithStep($"s3", c => { c.Item2 = "c"; })
            .Start();

        Assert.AreEqual("a", r.State.Item1?? "");
        Assert.AreEqual("b", r.State.Item2?? "");
    }

    [TestMethod]
    public async Task ValueTupleObjectManipulationWithReturn()
    {
        var r = await Job.JobFactory.New()
            .WithStep($"s1", async () => { await Task.Delay(5); return ("a", "b"); })
            .WithStep($"s2", c => { c.Item1 += "b"; return c; })
            .WithStep($"s3", c => { c.Item2 = "c"; return c; })
            .Start();

        Assert.AreEqual("ab", r.State.Item1?? "");
        Assert.AreEqual("c", r.State.Item2?? "");
    }

    [TestMethod]
    public async Task PostActionTest()
    {
        var x = 0;

        var r = await Job.JobFactory.New()
            .WithPostAction("pa1", _ => x++)
            .WithStep($"s1", async _ => { await Task.Delay(5); return ("a", "b"); })
            .WithStep($"s2", c => { c.Item1 += "b"; return c; })
            .WithStep($"s3", c => { c.Item2 = "c"; return c; })
            .Start();

        Assert.AreEqual(0, x);
        Assert.AreEqual("ab", r.State.Item1?? "");
        Assert.AreEqual("c", r.State.Item2?? "");

        await r.WithPostAction("pa2", _ => x++)
               .WithStep($"s4", c => { c.Item1 += "c"; return c; })
               .WithStep($"s5", c => { c.Item2 += "d"; return c; })
               .WithStep($"s6", c => c)
               .Start();

        Assert.AreEqual(3, x);
        Assert.AreEqual("abc", r.State.Item1?? "");
        Assert.AreEqual("cd", r.State.Item2?? "");
    }

    [TestMethod]
    public async Task IndependentPostActionTest()
    {
        var x = 0;

        var r = await Job.JobFactory.New()
            .WithPostAction("pa1", () => x++)
            .WithStep($"s1", _ => 1)
            .WithStep($"s2", c => (c + 1).ToString())
            .WithStep($"s3", c => new MyTypeC { S1 = c })
            .Start();

        Assert.AreEqual(3, x);
        Assert.AreEqual("2", r.State?.S1?? "");
        Assert.AreEqual("", r.State?.S2?? "");
        
        await r.WithPostAction("pa2", () => x++)
               .WithStep($"s4", c => { c.S1 += "3"; return c; })
               .WithStep($"s5", c => { c.S2 += "4"; return c; })
               .WithStep($"s6", c => c)
               .Start();

        Assert.AreEqual(9, x);
        Assert.AreEqual("23", r.State?.S1?? "");
        Assert.AreEqual("4", r.State?.S2?? "");

        await r.Remove("pa2").WithStep($"s7", c => c).Start();

        Assert.AreEqual(10, x);

        await r.RemoveAllPostActions().WithStep($"s8", c => c).Start();

        Assert.AreEqual(10, x);
    }

    [TestMethod]
    public async Task OnFinishTesting()
    {
        var x = 0;

        var r = await Job.JobFactory.New()
            .WithStep($"s1", _ => 1)
            .WithStep($"s1", _ => 2)
            .OnFinish("of1", i => { x = x + i; })
            .Start();

        Assert.AreEqual(2, x);
    }

    [TestMethod]
    public async Task DoubleOnFinishTesting()
    {
        var x = 0;

        var r = await Job.JobFactory.New()
            .WithStep($"s1", _ => 1)
            .WithStep($"s1", _ => 2)
            .OnFinish("of1", i => { x = x + i; })
            .Start();

        await r.Start();

        Assert.AreEqual(2, x);
    }
}
