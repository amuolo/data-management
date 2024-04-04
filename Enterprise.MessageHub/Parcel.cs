namespace Enterprise.MessageHub;

public record Parcel(object? Address, object? Package, string Message)
{
    public string Type { get; set; } = MessageType.SendMessage;

    public Guid Id { get; } = Guid.NewGuid();
}