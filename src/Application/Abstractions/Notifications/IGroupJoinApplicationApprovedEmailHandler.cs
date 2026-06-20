using Domain.Groups.Events;

namespace Application.Abstractions.Notifications;

public interface IGroupJoinApplicationApprovedEmailHandler
{
    Task HandleAsync(GroupJoinApplicationApproved domainEvent, CancellationToken cancellationToken = default);
}
