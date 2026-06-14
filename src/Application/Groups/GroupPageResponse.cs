using Domain.Groups;

namespace Application.Groups;

public sealed record GroupPageResponse(
    Guid Id,
    string Name,
    string Description,
    GroupJoinPolicy JoinPolicy,
    string ProfileImageUrl,
    DateTime CreatedAt,
    int MemberCount,
    GroupMemberRole? MyRole);
