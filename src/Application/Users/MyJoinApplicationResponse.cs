using Application.Groups;
using Domain.Groups;

namespace Application.Users;

public sealed record MyJoinApplicationResponse(
    Guid Id,
    Guid GroupId,
    string GroupName,
    GroupJoinApplicationStatus Status,
    DateTime SubmittedAt,
    DateTime? DecidedAt,
    Guid? DecidedByUserId);
