using Domain.Groups;

namespace Application.Groups;

public sealed record PublicGroupResponse(
    Guid Id,
    string Name,
    string Description,
    GroupJoinPolicy JoinPolicy,
    string ProfileImageUrl,
    DateTime CreatedAt,
    int MemberCount,
    bool IsVerified,
    GroupMemberRole? MyRole);
