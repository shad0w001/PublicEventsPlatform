using Domain.Events;

namespace Application.Abstractions.Search;

public interface IEventSemanticBrowseRerankService
{
    Task<List<Event>> GetBrowsePageAsync(
        IQueryable<Event> structuralQuery,
        string queryText,
        float[] queryEmbedding,
        int skip,
        int take,
        CancellationToken cancellationToken = default);
}
