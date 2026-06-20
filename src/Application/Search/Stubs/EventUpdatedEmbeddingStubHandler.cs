using Application.Abstractions.Data;
using Application.Abstractions.Search;
using Domain.Events;
using Domain.Events.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Search.Stubs;

internal sealed class EventUpdatedEmbeddingStubHandler(
    IApplicationDbContext context,
    ILogger<EventUpdatedEmbeddingStubHandler> logger) : IEventUpdatedEmbeddingStubHandler
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
                "Skipping embedding indexer stub for event {EventId}: not published, deleted, or missing",
                domainEvent.EventId);
            return;
        }

        logger.LogInformation(
            "Embedding indexer stub (Phase 9): event {EventId} updated",
            domainEvent.EventId);
    }
}
