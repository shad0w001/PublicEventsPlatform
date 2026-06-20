using Domain.Events.Events;

namespace Application.Abstractions.Notifications;

public interface IEventCancelledEmailHandler
{
    Task HandleAsync(EventCancelled domainEvent, CancellationToken cancellationToken = default);
}
