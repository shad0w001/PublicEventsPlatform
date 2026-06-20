using Application.Abstractions.Search;
using Domain.Events.Events;
using Microsoft.Extensions.Logging;

namespace Application.Search.Stubs;

internal sealed class EventPublishedEmbeddingStubHandler(
    ILogger<EventPublishedEmbeddingStubHandler> logger) : IEventPublishedEmbeddingStubHandler
{
    public Task HandleAsync(
        EventPublished domainEvent,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Embedding indexer stub (Phase 9): event {EventId} published (host participant {HostParticipantId})",
            domainEvent.EventId,
            domainEvent.HostParticipantId);

        return Task.CompletedTask;
    }
}
