using System.Collections.Immutable;
using System.Diagnostics;

namespace JobAgent;

public record Job()
{
    /* Public API */

    public static Job New() => new();

    public object? GetResult() => Result;

    public Job WithLogs(Action<string> logger) => this with { Logger = logger };

    public Job WithPostActions<T>(string name, Action<T> action) => this with { PostActions = PostActions.Add((name, action)) };

    public Job WithStep(string name, Delegate func) => this with { Steps = Steps.Add((name, func)) };

    public Task Start()
    {
        isValid = true;

        return Task.Run(async () =>
        {
            foreach (var step in Steps)
            {
                if (!isValid) return;

                await Execute(step.Name, step.Func);

                foreach (var post in PostActions)
                    await Execute(post.Name, post.Func);
            }
        });
    }

    /* Private */

    private bool isValid;

    private object? Result { get; set; }

    private Action<string> Logger { get; set; } = delegate { };

    private ImmutableList<(string Name, Delegate? Func)> PostActions { get; set; } = ImmutableList<(string, Delegate?)>.Empty;

    private ImmutableList<(string Name, Delegate? Func)> Steps { get; set; } = ImmutableList<(string, Delegate?)>.Empty;

    private async Task Execute(string name, Delegate? func)
    {
        try
        {
            if (func == null) throw new Exception("Process function cannot be null.");

            Stopwatch timer = new();
            timer.Start();

            dynamic task = Task.Run(() => Result == null ? func.DynamicInvoke() : func.DynamicInvoke(Result));
            Result = await task;

            timer.Stop();
            Logger?.Invoke($"{name} took {timer.ElapsedMilliseconds} ms");
        }
        catch (Exception ex)
        {
            Logger?.Invoke($"Exception caught when executing '{name}': {ex.InnerException?.Message?? ex.Message}");
            isValid = false;
        }
    }
}
