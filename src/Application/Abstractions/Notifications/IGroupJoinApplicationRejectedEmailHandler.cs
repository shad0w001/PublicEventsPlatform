using Domain.Groups.Events;

namespace Application.Abstractions.Notifications;

public interface IGroupJoinApplicationRejectedEmailHandler
{
    Task HandleAsync(GroupJoinApplicationRejected domainEvent, CancellationToken cancellationToken = default);
}
