﻿namespace JobAgent;

public record JobConfiguration()
{
    /* Public API */

    public JobConfiguration WithLogs(Action<string> logger) => this with { Logger = logger };

    public JobConfiguration WithProgressBar(Action<int> enable, Action update, Action close)
        => this with 
        {
            ProgressBarEnable = enable, 
            ProgressBarUpdate = update, 
            ProgressBarClose = close 
        };

    /* Internal */

    internal Action<string> Logger { get; set; } = delegate { };

    internal bool ShowProgress => ProgressBarEnable != null;

    internal Action<int>? ProgressBarEnable { get; set; }

    internal Action ProgressBarUpdate { get; set; } = delegate() { };

    internal Action ProgressBarClose { get; set; } = delegate() { };
}
