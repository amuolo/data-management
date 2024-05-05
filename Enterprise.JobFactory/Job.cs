using System.Collections.Concurrent;
using System.Diagnostics;

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

    public static Job<TState> New<TState>() => new Job<TState>().Initialize(default);

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

    public async Task<TState?> GetStateAsync(CancellationToken token = new())
    {
        await Semaphore.WaitAsync(token).ConfigureAwait(false);
        Semaphore.Release();
        return State;
    }

    public Job<TState> Initialize(TState? state) => this with { UseSubstitute = false, State = state };

    public Job<TState> OnFinish(string name, Action<TState> onFinish)
    {
        OnFinishAction.TryAdd(name, onFinish);
        return this;
    }

    public Job<TState> WithPostAction(string name, Action<TState> action)
    {
        PostActions.TryAdd(name, action);
        return this;
    }

    public Job<TState> WithPostAction(string name, Action action)
    {
        IndependentPostActions.TryAdd(name, action);
        return this;
    }

    public Job<TState> Remove(string name)
    {
        OnFinishAction.TryRemove(name, out var _);
        IndependentPostActions.TryRemove(name, out _);
        PostActions.TryRemove(name, out _); 
        return this;
    }

    public Job<TState> RemoveAllPostActions()
    {
        IndependentPostActions.Clear();
        PostActions.Clear();
        return this;
    }

    public virtual Job<TState> WithOptions(Func<JobConfiguration, JobConfiguration> update)
    {
        return this with { Configuration = update(Configuration) };
    }

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

    public async Task<Job<TState>> Start(CancellationToken token = new())
    {
        var progress = false;
        try
        {
            await Semaphore.WaitAsync(token).ConfigureAwait(false);

            if (!Steps.IsEmpty && Configuration.ProgressBarEnable is not null)
            {
                progress = true;
                Configuration.ProgressBarEnable(Steps.Count);
            }

            while (!Steps.IsEmpty && !token.IsCancellationRequested)
            {
                var ok = Steps.TryDequeue(out var step);

                if (!ok) throw new Exception("Issue with job dequeuing");

                await ExecuteAsync(step.Name, step.Func, step.TOrigin, step.TDestination).ConfigureAwait(false);

                foreach (var post in PostActions)
                    await ExecuteAsync(post.Key, post.Value, typeof(TState), null).ConfigureAwait(false);

                foreach (var post in IndependentPostActions)
                    await ExecuteAsync(post.Key, post.Value, null, null).ConfigureAwait(false);

                if (progress && Configuration.ProgressBarUpdate is not null)
                    Configuration.ProgressBarUpdate();
            }
        }
        catch (Exception ex)
        {
            var msg = $"Exception caught when executing '{CurrentStep}': {ex.Message}: {ex.InnerException?.Message?? ""}";
            if (Configuration.Logger is not null)
                Configuration.Logger.Invoke(msg);
            if (Configuration.AsyncLogger is not null)
                await Configuration.AsyncLogger.Invoke(msg).ConfigureAwait(false);
            if (Configuration.Logger is null && Configuration.AsyncLogger is null)
                throw;
        }
        finally
        {
            if (progress && Configuration.ProgressBarClose is not null)
                Configuration.ProgressBarClose();
            Semaphore.Release();
        }
        return this;
    }

    /* Private - Protected */

    protected bool UseSubstitute { get; set; } = true;

    protected object? Substitute { get; set; }

    SemaphoreSlim Semaphore { get; } = new(1, 1);

    protected string CurrentStep { get; set; } = string.Empty;

    protected JobConfiguration Configuration { get; set; } = new();

    protected ConcurrentDictionary<string, Delegate> OnFinishAction { get; set; } = [];

    protected ConcurrentDictionary<string, Delegate> PostActions { get; set; } = [];

    protected ConcurrentDictionary<string, Delegate> IndependentPostActions { get; set; } = [];

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
                UseSubstitute = false;
            }
            else if (TDestination == typeof(Task<TState>))
            {
                State = await ((Func<Task<TState>>)func)();
                UseSubstitute = false;
            }
            else
            {
                UseSubstitute = true;
                Substitute = func.DynamicInvoke();
                if (isGenericTask)
                {
                    if (Substitute is null)
                        throw new Exception(err);
                    await (Task)Substitute;
                    Substitute = Substitute.GetType().GetProperty("Result")?.GetValue(Substitute);
                }
            }
        }
        else 
        {
            if (TDestination is null)
            {
                if (TOrigin == typeof(TState) && !UseSubstitute)
                    ((Action<TState>)func)(State);
                else
                    func.DynamicInvoke(Substitute);
            }
            else if (TDestination == typeof(Task))
            {
                if (TOrigin == typeof(TState) && !UseSubstitute)
                    await ((Func<TState, Task>)func)(State);
                else
                {
                    var r = func.DynamicInvoke(Substitute);
                    if (r is null)
                        throw new Exception(err);
                    await (Task)r;
                }
            }
            else if (TDestination == typeof(TState))
            {
                if (TOrigin == typeof(TState) && !UseSubstitute)
                    State = ((Func<TState, TState>)func)(State);
                else
                    State = (TState?)func.DynamicInvoke(Substitute);
                UseSubstitute = false;
            }
            else if (TDestination == typeof(Task<TState>))
            {
                if (TOrigin == typeof(TState) && !UseSubstitute)
                    State = await ((Func<TState, Task<TState>>)func)(State);
                else
                {
                    var r = (Task<TState?>?)func.DynamicInvoke(Substitute);
                    if (r is null)
                        throw new Exception(err);
                    State = await r;
                }
                UseSubstitute = false;
            }
            else
            {
                if (TOrigin == typeof(TState) && !UseSubstitute)
                    Substitute = func.DynamicInvoke(State);
                else
                    Substitute = func.DynamicInvoke(Substitute);
                if (isGenericTask)
                {
                    if (Substitute is null)
                        throw new Exception(err);
                    await (Task)Substitute;
                    Substitute = Substitute.GetType().GetProperty("Result")?.GetValue(Substitute);
                }
                UseSubstitute = true;
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
            Substitute = !job.UseSubstitute ? job.State : job.Substitute,
            IndependentPostActions = job.IndependentPostActions,
            Configuration = job.Configuration,
            CurrentStep = job.CurrentStep,
            Steps = job.Steps
        };
}
