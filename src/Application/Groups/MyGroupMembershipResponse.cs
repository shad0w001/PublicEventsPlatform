using Domain.Groups;

namespace Application.Groups;

public sealed record MyGroupMembershipResponse(
    Guid Id,
    string Name,
    string ProfileImageUrl,
    GroupJoinPolicy JoinPolicy,
    int MemberCount,
    GroupMemberRole MyRole,
    DateTime JoinedAt);
