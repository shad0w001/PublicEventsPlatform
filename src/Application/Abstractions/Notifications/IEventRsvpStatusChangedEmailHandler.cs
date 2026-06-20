using Domain.Events.Events;

namespace Application.Abstractions.Notifications;

public interface IEventRsvpStatusChangedEmailHandler
{
    Task HandleAsync(EventRsvpStatusChanged domainEvent, CancellationToken cancellationToken = default);
}
