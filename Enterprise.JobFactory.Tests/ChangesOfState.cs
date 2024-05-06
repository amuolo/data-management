namespace Enterprise.JobFactory.Tests;

[TestClass]
public class ChangesOfStateType
{
    List<string> Storage { get; set; } = [];

    Action<string> Log => Storage.Add;

    [TestMethod]
    public async Task SeveralChangesOfStates()
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
    public async Task TrickyChangesOfState()
    {
        var r = await Job.JobFactory.New()
            .WithOptions(o => o.WithLogs(Log))
            .WithStep($"s1", () => 2)
            .WithStep($"s2", n1 => n1 + 0.1)
            .WithStep($"s3", n2 => (int)Math.Ceiling(n2))
            .Start();

        Assert.AreEqual(3, r.State);
        Assert.AreEqual(3, Storage.Count);
    }

    [TestMethod]
    public async Task TrickierChangesOfState()
    {
        var r = await Job.JobFactory.New()
            .WithStep($"s1", _ => 2)
            .WithStep($"s2", r1 => r1.ToString())
            .Start();

        Assert.AreEqual("2", r.State);
    }

    [TestMethod]
    public async Task SelectedChangesOfState()
    {
        var r = await Job.JobFactory.New(2)
            .WithOptions(o => o.WithLogs(Log))
            .WithStep($"s1", n => n.ToString() + "hello")
            .WithStep($"s2", n1 => int.Parse(n1.First().ToString()) + 2)
            .Start();

        Assert.AreEqual(4, r.State);
        Assert.AreEqual(2, Storage.Count);
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

    [TestMethod]
    public async Task ChangeOfStateQueueEffect()
    {
        var x = 0;

        var r = await Job.JobFactory.New(0)
            .WithPostAction("pa1", () => x++)
            .WithStep("s1", a => a + 1)
            .WithStep("s2", a => a + 2)
            .Start();

        Assert.AreEqual(3, r.State);
        Assert.AreEqual(2, x);

        var rr = await r.WithStep("s3", a => a.ToString()).Start();
        await r.Start();

        Assert.AreEqual("3", rr.State);
        Assert.AreEqual(3, r.State);
        Assert.AreEqual(3, x);
    }
}


