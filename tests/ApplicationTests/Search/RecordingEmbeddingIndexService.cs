using Application.Abstractions.Search;

namespace ApplicationTests.Search;

internal sealed class RecordingEmbeddingIndexService : IEventEmbeddingIndexService
{
    public List<Guid> UpsertedEventIds { get; } = [];

    public List<Guid> DeletedEventIds { get; } = [];

    public Task UpsertAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        UpsertedEventIds.Add(eventId);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        DeletedEventIds.Add(eventId);
        return Task.CompletedTask;
    }
}
