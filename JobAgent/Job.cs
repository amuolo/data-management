using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;

namespace JobAgent;

public static class JobFactory
{
    public static JobBase<Task> New() => new();
}

public record JobBase<T> : Job<T> where T : Task
{
    public JobBase<T> WithStep(string name, Action step) => this with { Steps = Steps.Add((name, step)) };

    public Job<TResult> WithStep<TResult>(string name, Func<TResult> step) => New<T, TResult>(this with { Steps = Steps.Add((name, step)) });

    public JobBase<T> WithOptions(Func<JobConfiguration, JobConfiguration> update) => this with { Configuration = update(Configuration) };
}

public record Job<T>()
{
    /* Public API */

    public object? GetResult() => Result;

    public Job<T> WithPostActions(string name, Action<T> action) => this with { PostActions = PostActions.Add((name, action)) };

    public Job<T> WithStep(string name, Action<T> step) => this with { Steps = Steps.Add((name, step)) };

    public Job<TResult> WithStep<TPrevious, TResult>(string name, Func<TPrevious, TResult> step) where TPrevious : T => New<T, TResult>(this) with { Steps = Steps.Add((name, step)) };

    public Task Start()
    {
        return Task.Run(async () =>
        {
            try
            {
                if (Configuration.ShowProgress) Configuration.ProgressBarEnable?.DynamicInvoke(Steps.Count);

                foreach (var step in Steps)
                {
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
        });
    }

    /* Private */

    protected string StepName { get; set; } = string.Empty;

    protected object? Result { get; set; }

    protected JobConfiguration Configuration { get; set; } = new();

    protected ImmutableList<(string Name, Delegate Func)> PostActions { get; set; } = ImmutableList<(string, Delegate)>.Empty;

    protected ImmutableList<(string Name, Delegate Func)> Steps { get; set; } = ImmutableList<(string, Delegate)>.Empty;

    protected async Task<bool> Execute(string name, Delegate func)
    {
        Stopwatch timer = new();
        StepName = name;
        timer.Start();

        dynamic task = Task.Run(() => func.GetMethodInfo().GetParameters().Any() ? func.DynamicInvoke(Result) : func.DynamicInvoke());
        Result = await task;

        timer.Stop();
        Configuration.Logger?.Invoke($"{name} took {timer.ElapsedMilliseconds} ms");
        return true;
    }

    protected static Job<TResult> New<TOrigin, TResult>(Job<TOrigin> job)
        => new Job<TResult>() with
        {
            Configuration = job.Configuration,
            StepName = job.StepName,
            Result = job.Result,
            PostActions = job.PostActions,
            Steps = job.Steps
        };
}
