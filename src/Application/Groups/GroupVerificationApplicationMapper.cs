using Domain.Groups;

namespace Application.Groups;

internal static class GroupVerificationApplicationMapper
{
    public static GroupVerificationApplicationResponse ToResponse(
        GroupVerificationApplication application,
        string groupName) =>
        new(
            application.Id,
            application.GroupId,
            groupName,
            application.SubmittedByUserId,
            application.Status,
            application.SubmittedAt,
            application.DecidedAt,
            application.DecidedByUserId);
}
