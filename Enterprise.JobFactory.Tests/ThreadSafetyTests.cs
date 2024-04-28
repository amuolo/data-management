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
            .WithStep("s1", () => new MyTypeX() { X = 0, Y = 0 });

        job.Start();

        Enumerable.Range(0, 10).ToList().ForEach(i =>
        {
            Task.Run(() => job.WithStep($"x{i}", s => { s.X++; s.Y--; }).Start());
        });

        await job.Start();

        Assert.AreEqual(+10, job.State?.X);
        Assert.AreEqual(-10, job.State?.Y);
    }

    [TestMethod]
    public async Task BasicSimultaneousIncrementsAsync()
    {
        var job = Job.JobFactory.New()
            .WithStep("s1", async () => { await Task.Delay(100); return new MyTypeX() { X = 0, Y = 0 }; });

        job.Start();

        Enumerable.Range(0, 10).ToList().ForEach(i =>
        {
            Task.Run(() => job.WithStep($"x{i}", async s => { await Task.Delay(10); s.X++; s.Y--; }).Start());
        });

        await job.Start();

        Assert.AreEqual(+10, job.State?.X);
        Assert.AreEqual(-10, job.State?.Y);
    }

    [TestMethod]
    public async Task BasicSimultaneousIncrementsAsyncWithPrimitiveType()
    {
        var job = Job.JobFactory.New()
            .WithStep("s1", async () => { await Task.Delay(100); return 0; });

        job.Start();

        Enumerable.Range(0, 10).ToList().AsParallel().ForAll(i =>
        {
            job.WithStep($"x{i}", async n => { await Task.Delay(10); return n++; }).Start();
        });

        await job.Start();

        // TODO: check if intended behavior, i.e. if use case exists for implementing state update for value types
        Assert.AreEqual(0, job.State);
    }
}
