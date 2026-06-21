using SharedKernel;

namespace Domain.Groups;

public static class GroupErrors
{
    public static Error NotFound(Guid groupId) => Error.NotFound(
        "Groups.NotFound",
        $"The group with the Id = '{groupId}' was not found");

    public static Error Deleted(Guid groupId) => Error.NotFound(
        "Groups.Deleted",
        $"The group with the Id = '{groupId}' has been deleted");

    public static Error InsufficientPermissions() => Error.Forbidden(
        "Groups.InsufficientPermissions",
        "Your role does not permit this action");

    public static readonly Error InvalidName = Error.Validation(
        "Groups.InvalidName",
        "Group name is required");

    public static Error NameTooLong(int maxLength) => Error.Validation(
        "Groups.NameTooLong",
        $"Group name must not exceed {maxLength} characters");

    public static readonly Error NotOpenForJoin = Error.Conflict(
        "Groups.NotOpenForJoin",
        "This group does not accept open join requests");

    public static readonly Error ApplicationsNotRequired = Error.Conflict(
        "Groups.ApplicationsNotRequired",
        "This group does not require join applications");

    public static readonly Error PendingApplicationExists = Error.Conflict(
        "Groups.PendingApplicationExists",
        "A pending join application already exists for this user");

    public static readonly Error ReapplyCooldownActive = Error.Conflict(
        "Groups.ReapplyCooldownActive",
        "You must wait before submitting another join application");

    public static readonly Error CannotTransferOwnershipToSelf = Error.Validation(
        "Groups.CannotTransferOwnershipToSelf",
        "Ownership cannot be transferred to yourself");

    public static readonly Error CannotTransferOwnershipToNonMember = Error.Validation(
        "Groups.CannotTransferOwnershipToNonMember",
        "Ownership can only be transferred to an existing group member");

    public static readonly Error NotOwner = Error.Forbidden(
        "Groups.NotOwner",
        "Only the group owner can perform this action");

    public static readonly Error AlreadyMember = Error.Conflict(
        "Groups.AlreadyMember",
        "You are already a member of this group");

    public static readonly Error OwnerCannotLeave = Error.Conflict(
        "Groups.OwnerCannotLeave",
        "The owner cannot leave the group without transferring ownership first");

    public static readonly Error NotMember = Error.Forbidden(
        "Groups.NotMember",
        "You are not a member of this group");

    public static readonly Error TargetNotMember = Error.NotFound(
        "Groups.TargetNotMember",
        "The specified user is not a member of this group");

    public static readonly Error CannotChangeOwnerRole = Error.Conflict(
        "Groups.CannotChangeOwnerRole",
        "Owner role changes must use transfer ownership");

    public static readonly Error CannotRemoveMember = Error.Forbidden(
        "Groups.CannotRemoveMember",
        "You cannot remove a member with this role");

    public static readonly Error NoFieldsToUpdate = Error.Validation(
        "Groups.NoFieldsToUpdate",
        "At least one field must be provided to update the group");

    public static readonly Error CannotDemoteSelf = Error.Forbidden(
        "Groups.CannotDemoteSelf",
        "You cannot demote your own role");

    public static readonly Error AlreadyVerified = Error.Conflict(
        "Groups.AlreadyVerified",
        "This organization is already verified");

    public static readonly Error VerificationPendingApplicationExists = Error.Conflict(
        "Groups.VerificationPendingApplicationExists",
        "A pending verification application already exists for this organization");

    public static readonly Error VerificationReapplyCooldownActive = Error.Conflict(
        "Groups.VerificationReapplyCooldownActive",
        "You must wait before submitting another verification application");

    public static readonly Error InsufficientVerifiedMembers = Error.Conflict(
        "Groups.InsufficientVerifiedMembers",
        $"At least {GroupVerificationConstants.MinVerifiedMembers} members with verified email are required to apply for verification");

    public static readonly Error InsufficientCompletedEvents = Error.Conflict(
        "Groups.InsufficientCompletedEvents",
        $"At least {GroupVerificationConstants.MinCompletedEvents} completed published events are required to apply for verification");
}
