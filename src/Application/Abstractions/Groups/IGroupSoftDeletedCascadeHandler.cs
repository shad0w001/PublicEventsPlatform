using Domain.Groups.Events;

namespace Application.Abstractions.Groups;

public interface IGroupSoftDeletedCascadeHandler
{
    Task HandleAsync(GroupSoftDeleted domainEvent, CancellationToken cancellationToken = default);
}
