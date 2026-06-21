using Domain.Groups.Events;

namespace Application.Abstractions.Notifications;

public interface IGroupVerificationApplicationApprovedEmailHandler
{
    Task HandleAsync(
        GroupVerificationApplicationApproved domainEvent,
        CancellationToken cancellationToken = default);
}
