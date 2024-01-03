using Agency;
using DataDomain;

namespace DataAgent;

public record DataChangedArgs();

public class DataAgent : Agent<Model>
{
    public event EventHandler<DataChangedArgs> DataChanged;

}
