using System.Collections.Immutable;
using System.Diagnostics;

namespace JobAgent;

public record Job()
{
    /* Public API */

    public static Job New() => new();

    public object? GetResult() => Result;

    public Job WithOptions(Func<JobConfiguration, JobConfiguration> update) => this with { Configuration = update(Configuration) };

    public Job WithPostActions<T>(string name, Action<T> action) => this with { PostActions = PostActions.Add((name, action)) };

    public Job WithStep(string name, Delegate func) => this with { Steps = Steps.Add((name, func)) };

    public Task Start()
    {
        return Task.Run(async () =>
        {
            foreach (var step in Steps)
            {
                if(!await Execute(step.Name, step.Func)) return;

                foreach (var post in PostActions)
                    if(!await Execute(post.Name, post.Func)) return;
            }
        });
    }

    /* Private */

    private object? Result { get; set; }

    private JobConfiguration Configuration { get; set; } = new();

    private ImmutableList<(string Name, Delegate? Func)> PostActions { get; set; } = ImmutableList<(string, Delegate?)>.Empty;

    private ImmutableList<(string Name, Delegate? Func)> Steps { get; set; } = ImmutableList<(string, Delegate?)>.Empty;

    private async Task<bool> Execute(string name, Delegate? func)
    {
        try
        {
            if (func == null) throw new Exception("Process function cannot be null.");

            Stopwatch timer = new();
            timer.Start();

            dynamic task = Task.Run(() => Result == null ? func.DynamicInvoke() : func.DynamicInvoke(Result));
            Result = await task;

            timer.Stop();
            Configuration.Logger?.Invoke($"{name} took {timer.ElapsedMilliseconds} ms");
            return true;
        }
        catch (Exception ex)
        {
            Configuration.Logger?.Invoke($"Exception caught when executing '{name}': {ex.InnerException?.Message?? ex.Message}");
            return false;
        }
    }
}
