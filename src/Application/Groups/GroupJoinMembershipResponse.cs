using Domain.Groups;

namespace Application.Groups;

public sealed record GroupJoinMembershipResponse(
    Guid UserId,
    GroupMemberRole Role,
    DateTime JoinedAt);
