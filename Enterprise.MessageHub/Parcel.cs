namespace Enterprise.MessageHub;

public record Parcel(object? Target, string? TargetId, object? Item, string Message)
{
    public string Type { get; set; } = nameof(PostingHub.SendMessage);

    public string Id { get; set; } = Guid.NewGuid().ToString();
}