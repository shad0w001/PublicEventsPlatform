namespace Application.Abstractions.Search;

public interface IEventEmbeddingIndexService
{
    Task UpsertAsync(Guid eventId, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid eventId, CancellationToken cancellationToken = default);
}
