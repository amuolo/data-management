using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;

namespace Job;

/// <summary>
/// Tasks handler made handy via easy-to-use api and commodities such as logs and progress visualization
/// Example usage:
/// JobFactory.New().WithOptions(o => o.WithLogs(Logger).WithProgress(progressBar.Enable, progressBar.Update, progressBar.Disable))
///                 .WithStep($"Import", () => State.Data.FirstStep())
///                 .WithStep($"Processing", () => State.Data.SecondStep())
///                 .WithStep($"Update Window", () => State.DataWindow.LastStep())
///                 .Start();
/// </summary>

public static class JobFactory
{
    public static Job New() => new();

    public static Job<T> New<T>(T initialState) => new Job<T>().Initialize(initialState);
}

public record Job : Job<Task>
{
    public Job WithStep(string name, Action step)
    {
        Steps.Enqueue((name, step));
        return this;
    }

    public Job<TResult> WithStep<TResult>(string name, Func<TResult> step)
    { 
        Steps.Enqueue((name, step)); 
        return New<Task, TResult>(this);
    }

    public override Job WithOptions(Func<JobConfiguration, JobConfiguration> update) => this with { Configuration = update(Configuration) };
}

public record Job<T>()
{
    /* Public API */

    public T? State { get; set; }

    public Job<T> Initialize(T? state) => this with { State = state };

    public Job<T> WithPostActions(string name, Action<T> action) => this with { PostActions = PostActions.Add((name, action)) };

    public virtual Job<T> WithOptions(Func<JobConfiguration, JobConfiguration> update) => this with { Configuration = update(Configuration) };

    public Job<T> WithStep(string name, Action<T> step) 
    { 
        Steps.Enqueue((name, step)); 
        return this; 
    }

    public Job<TResult> WithStep<TPrevious, TResult>(string name, Func<TPrevious, TResult> step) where TPrevious : T 
    { 
        Steps.Enqueue((name, step)); 
        return New<T, TResult>(this); 
    }

    public Task<Job<T>> Start()
    {
        return Task.Run(async () =>
        {
            try
            {
                if (Configuration.ShowProgress) Configuration.ProgressBarEnable?.DynamicInvoke(Steps.Count);

                while (!Steps.IsEmpty)
                {
                    var ok = Steps.TryDequeue(out var step);

                    if (!ok) throw new Exception("Issue with job dequeueing");

                    await Execute(step.Name, step.Func);

                    foreach (var post in PostActions)
                        await Execute(post.Name, post.Func);

                    if (Configuration.ShowProgress) Configuration.ProgressBarUpdate();
                }
            }
            catch (Exception ex)
            {
                Configuration.Logger?.Invoke($"Exception caught when executing '{StepName}': {ex.InnerException?.Message?? ex.Message}");
            }

            if (Configuration.ShowProgress) Configuration.ProgressBarClose();

            return this;
        });
    }

    /* Private */

    private readonly SemaphoreSlim semaphore = new(0);

    protected string StepName { get; set; } = string.Empty;

    protected JobConfiguration Configuration { get; set; } = new();

    protected ImmutableList<(string Name, Delegate Func)> PostActions { get; set; } = ImmutableList<(string, Delegate)>.Empty;

    protected ConcurrentQueue<(string Name, Delegate Func)> Steps { get; set; } = new();

    protected async Task<bool> Execute(string name, Delegate func)
    {
        Stopwatch timer = new();
        StepName = name;
        timer.Start();

        var task = Task.Run(() => func.GetMethodInfo().GetParameters().Any() ? func.DynamicInvoke(State) : func.DynamicInvoke());
        State = (T?)await task;

        timer.Stop();
        Configuration.Logger?.Invoke($"{name} took {timer.ElapsedMilliseconds} ms");
        return true;
    }

    protected static Job<TResult> New<TOrigin, TResult>(Job<TOrigin> job)
        => new Job<TResult>() with
        {
            Configuration = job.Configuration,
            StepName = job.StepName,
            PostActions = job.PostActions,
            Steps = job.Steps
        };
}
