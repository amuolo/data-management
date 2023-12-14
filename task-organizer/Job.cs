using System.Collections.Immutable;
using System.Diagnostics;

namespace TaskOrganizer;

public record Job()
{
    private bool isValid;

    private Action<string> Logger { get; set; } = delegate { };

    private ImmutableList<(string Name, Delegate? Func)> PostActions { get; set; } = ImmutableList<(string, Delegate?)>.Empty;

    private ImmutableList<(string Name, Delegate? Func)> Steps { get; set; } = ImmutableList<(string, Delegate?)>.Empty;

    public static Job New() => new();

    internal Job WithLogs(Action<string> logger) => this with { Logger = logger };

    internal Job WithPostActions<T>(string name, Action<T> action) => this with { PostActions = PostActions.Add((name, action)) };

    internal Job WithStep(string name, Delegate func) => this with { Steps = Steps.Add((name, func)) };

    internal Task Start()
    {
        isValid = true;

        return Task.Run(async () =>
        {
            object? result = default;

            foreach (var step in Steps)
            {
                if (!isValid) return;

                await Execute(step.Name, step.Func, result);

                foreach(var post in PostActions)
                    await Execute(post.Name, post.Func, result);
            }
        });
    }

    private async Task Execute(string name, Delegate? func, object? result)
    {
        try
        {
            if (func == null) throw new Exception("Process function cannot be null.");

            Stopwatch timer = new();
            timer.Start();

            dynamic task = Task.Run(() => result == null ? func.DynamicInvoke() : func.DynamicInvoke(result));
            result = await task;

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
