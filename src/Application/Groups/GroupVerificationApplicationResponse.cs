using Domain.Groups;

namespace Application.Groups;

public sealed record GroupVerificationApplicationResponse(
    Guid Id,
    Guid GroupId,
    string GroupName,
    Guid SubmittedByUserId,
    GroupVerificationApplicationStatus Status,
    DateTime SubmittedAt,
    DateTime? DecidedAt,
    Guid? DecidedByUserId);
