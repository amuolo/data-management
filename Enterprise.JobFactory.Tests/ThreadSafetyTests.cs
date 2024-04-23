namespace Enterprise.JobFactory.Tests;

[TestClass]
public class ThreadSafetyTests
{
    public record MyTypeX
    {
        public int X { get; set; }
        public int Y { get; set; }
    }

    [TestMethod]
    public async Task BasicSimultaneousIncrements()
    {
        var job = Job.JobFactory.New()
            .WithStep("s1", async () => { await Task.Delay(100); return new MyTypeX() { X = 0, Y = 0 }; });

        job.Start();

        Enumerable.Range(0, 10).ToList().ForEach(i =>
        {
            Task.Run(() => job.WithStep($"x{i}", async s => { await Task.Delay(10 + i); s.X++; s.Y--; }).Start());
        });

        await job.Start();

        Assert.AreEqual(+10, job.State?.X);
        Assert.AreEqual(-10, job.State?.Y);
    }
}
