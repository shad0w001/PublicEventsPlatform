using Application.Abstractions.Data;
using Application.Abstractions.Search;
using Application.Events.Services;
using Domain.Events;
using Domain.Search;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Search;

internal sealed class EventSemanticBrowseRerankService(IApplicationDbContext context)
    : IEventSemanticBrowseRerankService
{
    public async Task<List<Event>> GetBrowsePageAsync(
        IQueryable<Event> structuralQuery,
        string queryText,
        float[] queryEmbedding,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        if (take <= 0)
        {
            return [];
        }

        if (IsInMemoryDatabase())
        {
            return await GetInMemoryPageAsync(
                structuralQuery,
                queryText,
                skip,
                take,
                cancellationToken);
        }

        return await GetPostgreSqlPageAsync(
            structuralQuery,
            queryText,
            queryEmbedding,
            skip,
            take,
            cancellationToken);
    }

    private async Task<List<Event>> GetInMemoryPageAsync(
        IQueryable<Event> structuralQuery,
        string queryText,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var filtered = EventTextSearchHelper.ApplyContainsFilter(structuralQuery, queryText);

        return await filtered
            .Include(e => e.Organizers)
            .Include(e => e.Category)
            .OrderBy(e => e.StartTime)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    private async Task<List<Event>> GetPostgreSqlPageAsync(
        IQueryable<Event> structuralQuery,
        string queryText,
        float[] queryEmbedding,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var allIds = await structuralQuery
            .OrderBy(e => e.StartTime)
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);

        if (allIds.Count == 0)
        {
            return [];
        }

        var headIds = allIds.Take(SearchConstants.MaxSemanticRerankCandidates).ToList();
        var tailIds = allIds.Skip(SearchConstants.MaxSemanticRerankCandidates).ToList();

        var orderedHeadIds = await RankHeadEventIdsAsync(
            structuralQuery,
            headIds,
            queryText,
            queryEmbedding,
            cancellationToken);

        var orderedIds = orderedHeadIds.Concat(tailIds).ToList();
        var pageIds = orderedIds.Skip(skip).Take(take).ToList();

        if (pageIds.Count == 0)
        {
            return [];
        }

        var eventsById = await structuralQuery
            .Where(e => pageIds.Contains(e.Id))
            .Include(e => e.Organizers)
            .Include(e => e.Category)
            .ToDictionaryAsync(e => e.Id, cancellationToken);

        return pageIds
            .Where(eventsById.ContainsKey)
            .Select(id => eventsById[id])
            .ToList();
    }

    private async Task<List<Guid>> RankHeadEventIdsAsync(
        IQueryable<Event> structuralQuery,
        IReadOnlyList<Guid> headIds,
        string queryText,
        float[] queryEmbedding,
        CancellationToken cancellationToken)
    {
        if (headIds.Count == 0)
        {
            return [];
        }

        var headEvents = await structuralQuery
            .Where(e => headIds.Contains(e.Id))
            .Include(e => e.Category)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var embeddings = await context.EventEmbeddings
            .AsNoTracking()
            .Where(emb => headIds.Contains(emb.EventId))
            .ToDictionaryAsync(emb => emb.EventId, emb => emb.Embedding, cancellationToken);

        return headEvents
            .Select(e => new
            {
                e.Id,
                e.StartTime,
                SortDistance = embeddings.TryGetValue(e.Id, out var embedding)
                    ? CosineSimilarity.Distance(embedding, queryEmbedding)
                    : EventTextSearchHelper.MatchesText(e, queryText)
                        ? SearchConstants.ContainsFallbackCosineDistance
                        : SearchConstants.NoMatchCosineDistance
            })
            .OrderBy(x => x.SortDistance)
            .ThenBy(x => x.StartTime)
            .Select(x => x.Id)
            .ToList();
    }

    private bool IsInMemoryDatabase() =>
        context is DbContext dbContext &&
        dbContext.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory";
}
