namespace Enterprise.Agency;

public class TimeSpans
{
    public static readonly TimeSpan HireAgentsPeriod = TimeSpan.FromMinutes(30);

    public static readonly TimeSpan ActorConnectionAttemptPeriod = TimeSpan.FromSeconds(3);
}
