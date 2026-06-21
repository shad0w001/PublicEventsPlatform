using Domain.Events;

namespace Domain.Search;

public sealed class EventEmbedding
{
    public Guid EventId { get; internal set; }
    public float[] Embedding { get; internal set; } = [];
    public DateTime UpdatedAt { get; internal set; }

    public Event Event { get; internal set; } = null!;
}
