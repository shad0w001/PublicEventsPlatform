using Domain.Groups;

namespace Application.Groups.CreateGroup;

public sealed record GroupResponse(
    Guid Id,
    string Name,
    string Description,
    GroupJoinPolicy JoinPolicy,
    string ProfileImageUrl,
    DateTime CreatedAt);
