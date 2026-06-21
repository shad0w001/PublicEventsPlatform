using Application.Abstractions.Search;
using Application.Search.Services;
using Domain.Events.Events;

namespace Application.Search;

internal sealed class EventCancelledEmbeddingHandler(
    IEventEmbeddingIndexService indexService) : IEventCancelledEmbeddingHandler
{
    public Task HandleAsync(
        EventCancelled domainEvent,
        CancellationToken cancellationToken = default) =>
        indexService.DeleteAsync(domainEvent.EventId, cancellationToken);
}
