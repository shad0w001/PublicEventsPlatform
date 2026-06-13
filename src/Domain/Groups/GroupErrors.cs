using SharedKernel;

namespace Domain.Groups;

public static class GroupErrors
{
    public static Error NotFound(Guid groupId) => Error.NotFound(
        "Groups.NotFound",
        $"The group with the Id = '{groupId}' was not found");

    public static Error Forbidden() => Error.Forbidden(
        "Groups.Forbidden",
        "You are not authorized to perform this action on this group");

    public static Error InsufficientPermissions() => Error.Forbidden(
        "Groups.InsufficientPermissions",
        "Your role does not permit this action");

    public static readonly Error AlreadyMember = Error.Conflict(
        "Groups.AlreadyMember",
        "You are already a member of this group");

    public static readonly Error OwnerCannotLeave = Error.Conflict(
        "Groups.OwnerCannotLeave",
        "The owner cannot leave the group without transferring ownership first");
}
