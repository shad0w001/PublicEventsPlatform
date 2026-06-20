using Domain.Groups.Events;

namespace Application.Abstractions.Notifications;

public interface IGroupJoinApplicationSubmittedEmailHandler
{
    Task HandleAsync(GroupJoinApplicationSubmitted domainEvent, CancellationToken cancellationToken = default);
}
