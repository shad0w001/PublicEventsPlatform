using Domain.Tickets.Events;

namespace Application.Abstractions.Notifications;

public interface ITicketPurchaseCompletedEmailHandler
{
    Task HandleAsync(TicketPurchaseCompleted domainEvent, CancellationToken cancellationToken = default);
}
