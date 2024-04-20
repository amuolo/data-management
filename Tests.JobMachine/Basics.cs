using Enterprise.Job;

namespace Tests.JobMachine;

[TestClass]
public class TestsJobMachine
{
    [TestMethod]
    public async Task SimpleStateManipulations()
    {
        var r = await JobFactory.New(-1)
            //.WithOptions(o => o.WithLogs(Logger).WithProgress(progressBar.Enable, progressBar.Update, progressBar.Disable))
            .WithStep($"s1", n0 => 1)
            .WithStep($"s2", n1 => n1 + 10)
            .WithStep($"s3", n2 => n2 + 20)
            .Start();

        Assert.AreEqual(31, r.State);
    }

    [TestMethod]
    public async Task ChangeOfStateFromNull()
    { 
        var r = await JobFactory.New()
            .WithStep($"s1", () => 1)
            .WithStep($"s2", n1 => n1 + 2)
            .WithStep($"s3", n2 => n2 + 3)
            .Start();

        Assert.AreEqual(6, r.State);
    }
}
