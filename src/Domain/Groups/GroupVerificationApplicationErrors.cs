using SharedKernel;

namespace Domain.Groups;

public static class GroupVerificationApplicationErrors
{
    public static Error NotFound(Guid applicationId) => Error.NotFound(
        "GroupVerificationApplications.NotFound",
        $"The verification application with the Id = '{applicationId}' was not found");

    public static readonly Error NotPending = Error.Conflict(
        "GroupVerificationApplications.NotPending",
        "The verification application is not in a pending state");
}
