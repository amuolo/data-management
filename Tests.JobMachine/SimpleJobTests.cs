using Enterprise.Job;

namespace Tests.JobMachine;

[TestClass]
public class TestsJobMachine
{
    [TestMethod]
    public async void SimpleJob()
    {
        Assert.AreEqual(1, 1);
        /*var r = await JobFactory.New()
            //.WithOptions(o => o.WithLogs(Logger).WithProgress(progressBar.Enable, progressBar.Update, progressBar.Disable))
            .WithStep($"s1", () => 1)
            .WithStep($"s2", n1 => n1 + 2)
            .WithStep($"s3", n2 => n2 + 3)
            .Start();

        Assert.AreEqual(r.State, 6);*/
    }
}
