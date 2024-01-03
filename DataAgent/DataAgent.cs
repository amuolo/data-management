using AgentContract;

namespace DataAgent;

public record DataChangedArgs();

public class DataAgent : Agent
{
    public event EventHandler<DataChangedArgs> DataChanged;

}
