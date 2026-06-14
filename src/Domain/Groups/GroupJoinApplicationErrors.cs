using SharedKernel;

namespace Domain.Groups;

public static class GroupJoinApplicationErrors
{
    public static Error NotFound(Guid applicationId) => Error.NotFound(
        "GroupJoinApplications.NotFound",
        $"The join application with the Id = '{applicationId}' was not found");

    public static readonly Error NotPending = Error.Conflict(
        "GroupJoinApplications.NotPending",
        "The join application is not in a pending state");

    public static readonly Error NotApplicant = Error.Forbidden(
        "GroupJoinApplications.NotApplicant",
        "Only the applicant can cancel this join application");
}
