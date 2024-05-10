namespace Enterprise.Job;

public record JobOptions()
{
    /* Public API */

    public JobOptions WithLogs(Action<string> logger) => this with { Logger = logger };

    public JobOptions WithAsyncLogs(Func<string, Task> logger) => this with { AsyncLogger = logger };

    public JobOptions ClearLogs()
        => this with
        {
            Logger = null, 
            AsyncLogger = null
        };

    public JobOptions WithProgress(Action<int> enable, Action update, Action close)
        => this with 
        {
            ProgressBarEnable = enable, 
            ProgressBarUpdate = update, 
            ProgressBarClose = close 
        };

    public JobOptions ClearProgress()
        => this with
        {
            ProgressBarEnable = null,
            ProgressBarUpdate = null,
            ProgressBarClose = null,
        };

    public JobOptions Clear() => ClearLogs().ClearProgress();

    /* Internal */

    internal Action<string>? Logger { get; set; }

    internal Func<string, Task>? AsyncLogger { get; set; }

    internal Action<int>? ProgressBarEnable { get; set; }

    internal Action? ProgressBarUpdate { get; set; }

    internal Action? ProgressBarClose { get; set; }
}
