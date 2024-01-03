namespace DataDomain;

public class Model
{
    public object Data { get; set; } = new object();

    public void Update(List<string> list)
    {
        Data = list;
    }

    public List<string> GetPrintable()
    {
        return Data as List<string>?? [];
    }

    public void Process()
    {
        // TODO
    }
}
