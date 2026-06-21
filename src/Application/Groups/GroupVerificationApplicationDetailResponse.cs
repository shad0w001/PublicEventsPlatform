using Domain.Groups;

namespace Application.Groups;

public sealed record GroupVerificationApplicationDetailResponse(
    Guid Id,
    Guid GroupId,
    string GroupName,
    string GroupDescription,
    string GroupProfileImageUrl,
    int MemberCount,
    bool GroupIsVerified,
    Guid SubmittedByUserId,
    GroupVerificationApplicationStatus Status,
    DateTime SubmittedAt,
    DateTime? DecidedAt,
    Guid? DecidedByUserId,
    GroupVerificationEligibilitySnapshot Eligibility);
