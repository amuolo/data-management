namespace Enterprise.JobFactory.Tests;

[TestClass]
public class ThreadSafety
{
    public record MyTypeX
    {
        public int X { get; set; }
        public int Y { get; set; }
    }

    [TestMethod]
    public async Task SimultaneousIncrements()
    {
        var job = Job.JobFactory.New()
            .WithStep("s1", () => new MyTypeX() { X = 0, Y = 0 });

        Enumerable.Range(0, 100).ToList().ForEach(i =>
        {
            Task.Run(() => job.WithStep($"x{i}", s => { s.X++; s.Y--; }).StartAsync());
        });

        await Task.Delay(100);
        await job.StartAsync();

        Assert.AreEqual(+100, job.State?.X);
        Assert.AreEqual(-100, job.State?.Y);
    }

    [TestMethod]
    public async Task SimultaneousIncrementsWithGetState()
    {
        var job = Job.JobFactory.New()
            .WithStep("s1", () => new MyTypeX() { X = 0, Y = 0 });

        Enumerable.Range(0, 100).ToList().ForEach(i =>
        {
            Task.Run(() => job.WithStep($"x{i}", s => { s.X++; s.Y--; }).StartAsync());
        });

        await Task.Delay(100);
        var res = await job.GetStateAsync();

        Assert.AreEqual(+100, res?.X);
        Assert.AreEqual(-100, res?.Y);
    }

    [TestMethod]
    public async Task SimultaneousIncrementsAsync()
    {
        var job = Job.JobFactory.New()
            .WithStep("s1", async () => { await Task.Delay(100); return new MyTypeX() { X = 0, Y = 0 }; });

        Enumerable.Range(0, 100).ToList().ForEach(i =>
        {
            Task.Run(() => job.WithStep($"x{i}", async s => { await Task.Delay(1); s.X++; s.Y--; }).StartAsync());
        });

        await job.StartAsync();

        Assert.AreEqual(+100, job.State?.X);
        Assert.AreEqual(-100, job.State?.Y);
    }

    [TestMethod]
    public async Task SimultaneousIncrementsAsyncWithPrimitiveType()
    {
        var job = Job.JobFactory.New()
            .WithStep("s1", async () => { await Task.Delay(100); return 0; });

        Enumerable.Range(0, 100).ToList().AsParallel().ForAll(i =>
        {
            job.WithStep($"x{i}", async n => { await Task.Delay(1); return ++n; }).StartAsync();
        });

        await job.StartAsync();

        Assert.AreEqual(100, job.State);
    }

    [TestMethod]
    public async Task ExtremeSimultaneousIncrementsWithPrimitiveType()
    {
        var job = Job.JobFactory.New()
            .WithStep("s1", async () => { await Task.Delay(100); return 0; });

        Enumerable.Range(0, 1000).ToList().AsParallel().ForAll(i =>
        {
            job.WithStep($"x{i}", n => i % 2 == 0 ? n + 1 : n - 1 ).StartAsync();
        });

        await job.StartAsync();

        Assert.AreEqual(0, job.State);
    }

    [TestMethod]
    public async Task ExtremeSimultaneousIncrementsWithStart()
    {
        var job = Job.JobFactory.New(0);
        var counter = 0;

        Enumerable.Range(0, 1000).ToList().AsParallel().ForAll(i =>
        {
            job.WithStep($"x{i}", n => { Interlocked.Increment(ref counter); return ++n; }).Start();
        });

        await job.StartAsync();

        Assert.AreEqual(1000, job.State);
        Assert.AreEqual(1000, counter);
    }
}
