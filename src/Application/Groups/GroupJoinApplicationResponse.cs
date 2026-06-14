using Domain.Groups;

namespace Application.Groups;

public sealed record GroupJoinApplicationResponse(
    Guid Id,
    Guid UserId,
    string? Username,
    GroupJoinApplicationStatus Status,
    DateTime SubmittedAt,
    DateTime? DecidedAt,
    Guid? DecidedByUserId);
