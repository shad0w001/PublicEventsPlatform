using SharedKernel;

namespace Domain.Users;

public static class UserErrors
{
    public static Error NotFound(Guid userId) => Error.NotFound(
        "Users.NotFound",
        $"The user with the Id = '{userId}' was not found");

    public static Error NotFoundByExternalSubjectId(string externalSubjectId) => Error.NotFound(
        "Users.NotFoundByExternalSubjectId",
        $"The user with ExternalSubjectId = '{externalSubjectId}' was not found");

    public static readonly Error EmailNotUnique = Error.Conflict(
        "Users.EmailNotUnique",
        "The provided email is not unique");

    public static readonly Error ExternalSubjectIdNotUnique = Error.Conflict(
        "Users.ExternalSubjectIdNotUnique",
        "The provided external subject id is not unique");

    public static Error Unauthorized() => Error.Failure(
        "Users.Unauthorized",
        "You are not authorized to perform this action");

    public static Error EmailNotVerified() => Error.Forbidden(
        "Users.EmailNotVerified",
        "Email verification is required to perform this action");
}
