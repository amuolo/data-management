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
            .WithOptions(o => o.WithLogs(Logger))//.WithProgress(progressBar.Enable, progressBar.Update, progressBar.Disable))
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
            .Start();

        Assert.AreEqual(9.9, r.State);
        Assert.AreEqual(4, Logs.Count);
    }
}
