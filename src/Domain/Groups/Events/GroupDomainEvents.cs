using SharedKernel;

namespace Domain.Groups.Events;

public sealed record GroupCreated(Guid GroupId, Guid CreatedByUserId) : DomainEvent;

public sealed record GroupProfileUpdated(Guid GroupId) : DomainEvent;

public sealed record GroupJoinPolicyChanged(Guid GroupId, GroupJoinPolicy NewPolicy) : DomainEvent;

public sealed record GroupSoftDeleted(Guid GroupId) : DomainEvent;

public sealed record GroupRestored(Guid GroupId) : DomainEvent;

public sealed record GroupMemberJoined(Guid GroupId, Guid UserId, GroupMemberRole Role) : DomainEvent;

public sealed record GroupMemberLeft(Guid GroupId, Guid UserId) : DomainEvent;

public sealed record GroupMemberRoleChanged(
    Guid GroupId,
    Guid UserId,
    GroupMemberRole PreviousRole,
    GroupMemberRole NewRole) : DomainEvent;

public sealed record GroupJoinApplicationSubmitted(Guid GroupId, Guid ApplicationId, Guid UserId) : DomainEvent;

public sealed record GroupJoinApplicationApproved(Guid GroupId, Guid ApplicationId, Guid UserId) : DomainEvent;

public sealed record GroupJoinApplicationRejected(Guid GroupId, Guid ApplicationId, Guid UserId) : DomainEvent;

public sealed record GroupJoinApplicationCancelled(Guid GroupId, Guid ApplicationId, Guid UserId) : DomainEvent;
