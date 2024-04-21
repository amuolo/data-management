using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;

namespace Enterprise.Job;

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

    public static Job<TState> New<TState>() where TState : new() => new Job<TState>().Initialize(new TState());

    public static Job<TState> New<TState>(TState initialState) => new Job<TState>().Initialize(initialState);
}

public record Job : Job<Task>
{
    public Job WithStep(string name, Action step)
    {
        Steps.Enqueue((name, step, null, null));
        return this;
    }

    public Job WithStep(string name, Func<Task> step)
    {
        Steps.Enqueue((name, step, null, typeof(Task)));
        return this;
    }

    public Job<TResult> WithStep<TResult>(string name, Func<TResult> step)
    {
        Steps.Enqueue((name, step, null, typeof(TResult)));
        return New<Task, TResult>(this);
    }

    public Job<TResult> WithStep<TResult>(string name, Func<Task<TResult>> step)
    {
        Steps.Enqueue((name, step, null, typeof(Task<TResult>)));
        return New<Task, TResult>(this);
    }

    public override Job WithOptions(Func<JobConfiguration, JobConfiguration> update) => this with { Configuration = update(Configuration) };
}

public record Job<TState>()
{
    /* Public API */

    public TState? State { get; set; }

    private object? TmpState { get; set; }

    public Job<TState> Initialize(TState? state) => this with { State = state };

    public Job<TState> WithPostActions(string name, Action<TState> action) => this with { PostActions = PostActions.Add((name, action)) };

    public virtual Job<TState> WithOptions(Func<JobConfiguration, JobConfiguration> update) => this with { Configuration = update(Configuration) };

    public Job<TState> WithStep(string name, Action<TState> step) 
    {
        Steps.Enqueue((name, step, typeof(TState), null));
        return this; 
    }

    public Job<TState> WithStep(string name, Func<TState, Task> step)
    {
        Steps.Enqueue((name, step, typeof(TState), typeof(Task)));
        return this;
    }

    public Job<TState> WithStep(string name, Func<TState, TState> step)
    {
        Steps.Enqueue((name, step, typeof(TState), typeof(TState)));
        return this;
    }

    public Job<TState> WithStep(string name, Func<TState, Task<TState>> step)
    {
        Steps.Enqueue((name, step, typeof(TState), typeof(Task<TState>)));
        return this;
    }

    public Job<TResult> WithStep<TResult>(string name, Func<TState, TResult> step)
    { 
        Steps.Enqueue((name, step, typeof(TState), typeof(TResult)));
        return New<TState, TResult>(this); 
    }

    public Job<TResult> WithStep<TResult>(string name, Func<TState, Task<TResult>> step)
    {
        Steps.Enqueue((name, step, typeof(TState), typeof(Task<TResult>)));
        return New<TState, TResult>(this);
    }

    public Task<Job<TState>> Start(CancellationToken token = new())
    {
        return Task.Run(async () =>
        {
            try
            {
                await Semaphore.WaitAsync(token).ConfigureAwait(false);

                if (Configuration.ShowProgress) Configuration.ProgressBarEnable?.DynamicInvoke(Steps.Count);

                while (!Steps.IsEmpty && !token.IsCancellationRequested)
                {
                    var ok = Steps.TryDequeue(out var step);

                    if (!ok) throw new Exception("Issue with job dequeuing");

                    await ExecuteAsync(step.Name, step.Func, step.TOrigin, step.TDestination).ConfigureAwait(false);

                    foreach (var post in PostActions)
                        await ExecuteAsync(post.Name, post.Func, typeof(TState), null).ConfigureAwait(false);

                    if (Configuration.ShowProgress) Configuration.ProgressBarUpdate();
                }
            }
            catch (Exception ex)
            {
                var msg = $"Exception caught when executing '{CurrentStep}': {ex.InnerException?.Message?? ex.Message}";
                if(Configuration.Logger is not null)
                    Configuration.Logger.Invoke(msg);
                if(Configuration.AsyncLogger is not null)
                    await Configuration.AsyncLogger.Invoke(msg).ConfigureAwait(false);
            }
            finally
            {
                if (Configuration.ShowProgress) Configuration.ProgressBarClose();
                Semaphore.Release();
            }

            return this;
        });
    }

    /* Private */

    SemaphoreSlim Semaphore { get; } = new(1, 1);

    protected string CurrentStep { get; set; } = string.Empty;

    protected JobConfiguration Configuration { get; set; } = new();

    protected ImmutableList<(string Name, Delegate Func)> PostActions { get; set; } = ImmutableList<(string, Delegate)>.Empty;

    protected ConcurrentQueue<(string Name, Delegate Func, Type? TOrigin, Type? TDestination)> Steps { get; set; } = new();

    protected async Task<bool> ExecuteAsync(string name, Delegate func, Type? TOrigin, Type? TDestination)
    {
        var err = $"Failed Job execution: return type does not match with result of function invocation for step {name}.";
        var timer = new Stopwatch();
        CurrentStep = name;
        timer.Start();

        var isGenericTask = (TDestination?.IsGenericType?? false) && TDestination.GetGenericTypeDefinition() == typeof(Task<>);

        if (TOrigin is null)
        {
            if (TDestination is null)
            {
                ((Action)func)();
            }
            else if (TDestination == typeof(Task))
            {
                await ((Func<Task>)func)();
            }
            else if (TDestination == typeof(TState))
            {
                State = ((Func<TState>)func)();
            }
            else if (TDestination == typeof(Task<TState>))
            {
                State = await ((Func<Task<TState>>)func)();
            }
            else
            {
                TmpState = func.DynamicInvoke();
                if (isGenericTask)
                {
                    if (TmpState is null)
                        throw new Exception(err);
                    await (Task)TmpState;
                    TmpState = TmpState.GetType().GetProperty("Result")?.GetValue(TmpState);
                }
            }
        }
        else 
        {
            if (TDestination is null)
            {
                if (TOrigin == typeof(TState))
                    ((Action<TState>)func)(State);
                else
                    func.DynamicInvoke(TmpState);
            }
            else if (TDestination == typeof(Task))
            {
                if (TOrigin == typeof(TState))
                    await ((Func<TState, Task>)func)(State);
                else
                {
                    var r = func.DynamicInvoke(TmpState);
                    if (r is null)
                        throw new Exception(err);
                    await (Task)r;
                }
            }
            else if (TDestination == typeof(TState))
            {
                if (TOrigin == typeof(TState))
                    State = ((Func<TState, TState>)func)(State);
                else
                    State = (TState?)func.DynamicInvoke(TmpState);
            }
            else if (TDestination == typeof(Task<TState>))
            {
                if (TOrigin == typeof(TState))
                    State = await ((Func<TState, Task<TState>>)func)(State);
                else
                {
                    var r = (Task<TState?>?)func.DynamicInvoke(TmpState);
                    if (r is null)
                        throw new Exception(err);
                    State = await r;
                }
            }
            else
            {
                if (TOrigin == typeof(TState))
                    TmpState = func.DynamicInvoke(State);
                else
                    TmpState = func.DynamicInvoke(TmpState);
                if (isGenericTask)
                {
                    if (TmpState is null)
                        throw new Exception(err);
                    await (Task)TmpState;
                    TmpState = TmpState.GetType().GetProperty("Result")?.GetValue(TmpState);
                }
            }
        }

        timer.Stop();
        var msg = $"{name} took {timer.ElapsedMilliseconds} ms";
        Configuration.Logger?.Invoke(msg);
        var log = Configuration.AsyncLogger?.Invoke(msg);
        if (log is not null) await log;
        return true;
    }

    protected static Job<TResult> New<TOrigin, TResult>(Job<TOrigin> job)
        => new Job<TResult>() with
        {
            TmpState = job.State,
            Configuration = job.Configuration,
            CurrentStep = job.CurrentStep,
            PostActions = job.PostActions,
            Steps = job.Steps
        };
}
