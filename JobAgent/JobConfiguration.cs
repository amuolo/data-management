namespace JobAgent;

public record JobConfiguration()
{
    public JobConfiguration WithLogs(Action<string> logger) => this with { Logger = logger };

    internal Action<string> Logger { get; set; } = delegate { };
}
