using Domain.Groups;

namespace Application.Groups;

public sealed record GroupMemberResponse(
    Guid UserId,
    string? Username,
    GroupMemberRole Role,
    DateTime JoinedAt);
