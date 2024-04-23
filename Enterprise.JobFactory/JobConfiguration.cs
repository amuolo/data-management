﻿namespace Enterprise.Job;

public record JobConfiguration()
{
    /* Public API */

    public JobConfiguration WithLogs(Action<string> logger) => this with { Logger = logger };

    public JobConfiguration WithAsyncLogs(Func<string, Task> logger) => this with { AsyncLogger = logger };

    public JobConfiguration ClearLogs()
        => this with
        {
            Logger = null, 
            AsyncLogger = null
        };

    public JobConfiguration WithProgress(Action<int> enable, Action update, Action close)
        => this with 
        {
            ProgressBarEnable = enable, 
            ProgressBarUpdate = update, 
            ProgressBarClose = close 
        };

    public JobConfiguration ClearProgress()
        => this with
        {
            ProgressBarEnable = null,
            ProgressBarUpdate = null,
            ProgressBarClose = null,
        };

    public JobConfiguration Clear() => ClearLogs().ClearProgress();

    /* Internal */

    internal Action<string>? Logger { get; set; }

    internal Func<string, Task>? AsyncLogger { get; set; }

    internal Action<int>? ProgressBarEnable { get; set; }

    internal Action? ProgressBarUpdate { get; set; }

    internal Action? ProgressBarClose { get; set; }
}
