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
}

public record Job : Job<Task>
{
    public Job WithStep(string name, Action step) => this with { Steps = Steps.Add((name, step)) };

    public Job<TResult> WithStep<TResult>(string name, Func<TResult> step) => New<Task, TResult>(this with { Steps = Steps.Add((name, step)) });

    public Job WithOptions(Func<JobConfiguration, JobConfiguration> update) => this with { Configuration = update(Configuration) };
}

public record Job<T>()
{
    /* Public API */

    public object? GetResult() => Result;

    public Job<T> WithPostActions(string name, Action<T> action) => this with { PostActions = PostActions.Add((name, action)) };

    public Job<T> WithStep(string name, Action<T> step) => this with { Steps = Steps.Add((name, step)) };

    public Job<TResult> WithStep<TPrevious, TResult>(string name, Func<TPrevious, TResult> step) where TPrevious : T => New<T, TResult>(this) with { Steps = Steps.Add((name, step)) };

    public Task<Job<T>> Start()
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

            return this;
        });
    }

    /* Private */

    protected string StepName { get; set; } = string.Empty;

    protected T? Result { get; set; }

    protected JobConfiguration Configuration { get; set; } = new();

    protected ImmutableList<(string Name, Delegate Func)> PostActions { get; set; } = ImmutableList<(string, Delegate)>.Empty;

    protected ImmutableList<(string Name, Delegate Func)> Steps { get; set; } = ImmutableList<(string, Delegate)>.Empty;

    protected async Task<bool> Execute(string name, Delegate func)
    {
        Stopwatch timer = new();
        StepName = name;
        timer.Start();

        var task = Task.Run(() => func.GetMethodInfo().GetParameters().Any() ? func.DynamicInvoke(Result) : func.DynamicInvoke());
        Result = (T?)await task;

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
