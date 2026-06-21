using Domain.Groups.Events;

namespace Application.Abstractions.Notifications;

public interface IGroupVerificationApplicationRejectedEmailHandler
{
    Task HandleAsync(
        GroupVerificationApplicationRejected domainEvent,
        CancellationToken cancellationToken = default);
}
