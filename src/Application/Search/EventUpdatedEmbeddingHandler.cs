using Application.Abstractions.Data;
using Application.Abstractions.Search;
using Application.Search.Services;
using Domain.Events;
using Domain.Events.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Search;

internal sealed class EventUpdatedEmbeddingHandler(
    IApplicationDbContext context,
    IEventEmbeddingIndexService indexService,
    ILogger<EventUpdatedEmbeddingHandler> logger) : IEventUpdatedEmbeddingHandler
{
    public async Task HandleAsync(
        EventUpdated domainEvent,
        CancellationToken cancellationToken = default)
    {
        var isPublished = await context.Events
            .AsNoTracking()
            .AnyAsync(
                e => e.Id == domainEvent.EventId &&
                     e.DeletedAt == null &&
                     e.Status == EventStatus.Published,
                cancellationToken);

        if (!isPublished)
        {
            logger.LogDebug(
                "Skipping embedding indexer for event {EventId}: not published, deleted, or missing",
                domainEvent.EventId);
            return;
        }

        await indexService.UpsertAsync(domainEvent.EventId, cancellationToken);
    }
}
