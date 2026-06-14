using Domain.Groups;

namespace Application.Groups;

internal static class GroupJoinApplicationMapper
{
    public static GroupJoinApplicationResponse ToResponse(
        Guid id,
        Guid userId,
        string? username,
        GroupJoinApplicationStatus status,
        DateTime submittedAt,
        DateTime? decidedAt,
        Guid? decidedByUserId) =>
        new(
            id,
            userId,
            username,
            status,
            submittedAt,
            decidedAt,
            decidedByUserId);

    public static GroupJoinApplicationResponse ToResponse(
        GroupJoinApplication application,
        string? username) =>
        ToResponse(
            application.Id,
            application.UserId,
            username,
            application.Status,
            application.SubmittedAt,
            application.DecidedAt,
            application.DecidedByUserId);
}
