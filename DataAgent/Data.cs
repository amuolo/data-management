using Agency;
using DataDomain;

namespace DataAgent;

public record DataChangedArgs();

public class Data : Agent<Model>
{
    public event EventHandler<DataChangedArgs> DataChanged;

}
