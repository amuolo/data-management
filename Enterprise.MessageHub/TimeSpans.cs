namespace Enterprise.MessageHub;

public class TimeSpans
{
    public static readonly TimeSpan ActorConnectionAttemptPeriod = TimeSpan.FromSeconds(3);

    public static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(60);
}
