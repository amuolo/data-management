using Enterprise.MessageHub;

namespace Enterprise.Agency;

internal record Parcel<IContract>(object? Address, object? Package, string Message)
{
    public string Type { get; set; } = Constants.SendMessage;

    public Guid Id { get; } = Guid.NewGuid();
}