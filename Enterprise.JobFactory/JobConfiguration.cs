namespace Enterprise.Job;

public record JobConfiguration()
{
    /* Public API */

    public JobConfiguration WithLogs(Action<string> logger) => this with { Logger = logger };

    public JobConfiguration WithAsyncLogs(Func<string, Task> logger) => this with { AsyncLogger = logger };

    public JobConfiguration WithProgress(Action<int> enable, Action update, Action close)
        => this with 
        {
            ProgressBarEnable = enable, 
            ProgressBarUpdate = update, 
            ProgressBarClose = close 
        };

    /* Internal */

    internal Action<string> Logger { get; set; } = delegate { };

    internal Func<string, Task> AsyncLogger { get; set; }

    internal bool ShowProgress => ProgressBarEnable != null;

    internal Action<int>? ProgressBarEnable { get; set; }

    internal Action ProgressBarUpdate { get; set; } = delegate() { };

    internal Action ProgressBarClose { get; set; } = delegate() { };
}
