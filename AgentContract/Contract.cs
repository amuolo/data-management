namespace Agency;

public interface IMessage
{
}

public record Log (DateTime Time, string User, string Message) : IMessage;


