namespace Agency;

public interface IAgencyContract
{
    Task Send(string message);
}