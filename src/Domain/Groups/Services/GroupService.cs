using Domain.Groups.Events;
using SharedKernel;

namespace Domain.Groups.Services;

public static class GroupService
{
    public static Result<(Group Group, GroupMembership OwnerMembership)> Create(
        string name,
        string? description,
        GroupJoinPolicy joinPolicy,
        Guid creatorUserId,
        string defaultImageUrl,
        string? profileImageUrl = null)
    {
        var nameResult = ValidateName(name);
        if (nameResult.IsFailure)
        {
            return Result.Failure<(Group, GroupMembership)>(nameResult.Error);
        }

        var group = new Group
        {
            Name = nameResult.Value,
            Description = NormalizeDescription(description),
            JoinPolicy = joinPolicy,
            ProfileImageUrl = profileImageUrl ?? defaultImageUrl
        };

        var ownerMembership = GroupMembership.Create(group.Id, creatorUserId, GroupMemberRole.Owner);
        group.GroupMemberships.Add(ownerMembership);

        group.Raise(new GroupCreated(group.Id, creatorUserId));
        group.Raise(new GroupMemberJoined(group.Id, creatorUserId, GroupMemberRole.Owner));

        return (group, ownerMembership);
    }

    public static Result UpdateProfile(Group group, string name, string? description, string? profileImageUrl)
    {
        var deletedResult = EnsureNotDeleted(group);
        if (deletedResult.IsFailure)
        {
            return deletedResult;
        }

        var nameResult = ValidateName(name);
        if (nameResult.IsFailure)
        {
            return nameResult;
        }

        group.Name = nameResult.Value;
        group.Description = NormalizeDescription(description);

        if (profileImageUrl is not null)
        {
            group.ProfileImageUrl = profileImageUrl;
        }

        group.Raise(new GroupProfileUpdated(group.Id));
        return Result.Success();
    }

    public static Result ChangeJoinPolicy(Group group, GroupJoinPolicy newPolicy)
    {
        var deletedResult = EnsureNotDeleted(group);
        if (deletedResult.IsFailure)
        {
            return deletedResult;
        }

        if (group.JoinPolicy == newPolicy)
        {
            return Result.Success();
        }

        group.JoinPolicy = newPolicy;
        group.JoinApplications.Clear();

        group.Raise(new GroupJoinPolicyChanged(group.Id, newPolicy));
        return Result.Success();
    }

    public static Result SoftDelete(Group group)
    {
        if (group.IsDeleted)
        {
            return Result.Success();
        }

        group.DeletedAt = DateTime.UtcNow;
        group.JoinApplications.Clear();

        group.Raise(new GroupSoftDeleted(group.Id));
        return Result.Success();
    }

    public static Result<GroupMembership> JoinOpen(Group group, Guid userId)
    {
        var deletedResult = EnsureNotDeleted(group);
        if (deletedResult.IsFailure)
        {
            return Result.Failure<GroupMembership>(deletedResult.Error);
        }

        if (group.JoinPolicy != GroupJoinPolicy.Open)
        {
            return Result.Failure<GroupMembership>(GroupErrors.NotOpenForJoin);
        }

        if (IsMember(group, userId))
        {
            return Result.Failure<GroupMembership>(GroupErrors.AlreadyMember);
        }

        var membership = GroupMembership.Create(group.Id, userId, GroupMemberRole.Member);
        group.GroupMemberships.Add(membership);

        group.Raise(new GroupMemberJoined(group.Id, userId, GroupMemberRole.Member));
        return membership;
    }

    public static Result<GroupJoinApplication> SubmitJoinApplication(
        Group group,
        Guid userId,
        DateTime utcNow)
    {
        var deletedResult = EnsureNotDeleted(group);
        if (deletedResult.IsFailure)
        {
            return Result.Failure<GroupJoinApplication>(deletedResult.Error);
        }

        if (group.JoinPolicy != GroupJoinPolicy.ApplicationRequired)
        {
            return Result.Failure<GroupJoinApplication>(GroupErrors.ApplicationsNotRequired);
        }

        if (IsMember(group, userId))
        {
            return Result.Failure<GroupJoinApplication>(GroupErrors.AlreadyMember);
        }

        if (HasPendingApplication(group, userId))
        {
            return Result.Failure<GroupJoinApplication>(GroupErrors.PendingApplicationExists);
        }

        var cooldownResult = ValidateReapplyCooldown(group, userId, utcNow);
        if (cooldownResult.IsFailure)
        {
            return Result.Failure<GroupJoinApplication>(cooldownResult.Error);
        }

        var application = CreatePendingApplication(group.Id, userId, utcNow);
        group.JoinApplications.Add(application);

        group.Raise(new GroupJoinApplicationSubmitted(group.Id, application.Id, userId));
        return application;
    }

    public static Result<GroupMembership> ApproveApplication(
        Group group,
        Guid applicationId,
        Guid decidedByUserId,
        DateTime utcNow)
    {
        var deletedResult = EnsureNotDeleted(group);
        if (deletedResult.IsFailure)
        {
            return Result.Failure<GroupMembership>(deletedResult.Error);
        }

        var application = FindApplication(group, applicationId);
        if (application is null)
        {
            return Result.Failure<GroupMembership>(GroupJoinApplicationErrors.NotFound(applicationId));
        }

        var approveResult = ApproveApplication(application, utcNow, decidedByUserId);
        if (approveResult.IsFailure)
        {
            return Result.Failure<GroupMembership>(approveResult.Error);
        }

        if (IsMember(group, application.UserId))
        {
            return Result.Failure<GroupMembership>(GroupErrors.AlreadyMember);
        }

        var membership = GroupMembership.Create(group.Id, application.UserId, GroupMemberRole.Member);
        group.GroupMemberships.Add(membership);

        group.Raise(new GroupJoinApplicationApproved(group.Id, application.Id, application.UserId));
        group.Raise(new GroupMemberJoined(group.Id, application.UserId, GroupMemberRole.Member));
        return membership;
    }

    public static Result RejectApplication(
        Group group,
        Guid applicationId,
        Guid decidedByUserId,
        DateTime utcNow)
    {
        var deletedResult = EnsureNotDeleted(group);
        if (deletedResult.IsFailure)
        {
            return deletedResult;
        }

        var application = FindApplication(group, applicationId);
        if (application is null)
        {
            return Result.Failure(GroupJoinApplicationErrors.NotFound(applicationId));
        }

        var rejectResult = RejectApplication(application, utcNow, decidedByUserId);
        if (rejectResult.IsFailure)
        {
            return rejectResult;
        }

        group.Raise(new GroupJoinApplicationRejected(group.Id, application.Id, application.UserId));
        return Result.Success();
    }

    public static Result CancelApplication(Group group, Guid applicationId, Guid userId)
    {
        var deletedResult = EnsureNotDeleted(group);
        if (deletedResult.IsFailure)
        {
            return deletedResult;
        }

        var application = FindApplication(group, applicationId);
        if (application is null)
        {
            return Result.Failure(GroupJoinApplicationErrors.NotFound(applicationId));
        }

        if (application.UserId != userId)
        {
            return Result.Failure(GroupJoinApplicationErrors.NotApplicant);
        }

        if (application.Status != GroupJoinApplicationStatus.Pending)
        {
            return Result.Failure(GroupJoinApplicationErrors.NotPending);
        }

        group.Raise(new GroupJoinApplicationCancelled(group.Id, application.Id, userId));
        group.JoinApplications.Remove(application);
        return Result.Success();
    }

    public static Result TransferOwnership(Group group, Guid currentOwnerUserId, Guid newOwnerUserId)
    {
        var deletedResult = EnsureNotDeleted(group);
        if (deletedResult.IsFailure)
        {
            return deletedResult;
        }

        if (currentOwnerUserId == newOwnerUserId)
        {
            return Result.Failure(GroupErrors.CannotTransferOwnershipToSelf);
        }

        var currentOwnerMembership = GetMembership(group, currentOwnerUserId);
        if (currentOwnerMembership is null || currentOwnerMembership.Role != GroupMemberRole.Owner)
        {
            return Result.Failure(GroupErrors.NotOwner);
        }

        var newOwnerMembership = GetMembership(group, newOwnerUserId);
        if (newOwnerMembership is null)
        {
            return Result.Failure(GroupErrors.CannotTransferOwnershipToNonMember);
        }

        var previousNewOwnerRole = newOwnerMembership.Role;
        newOwnerMembership.Role = GroupMemberRole.Owner;
        currentOwnerMembership.Role = GroupMemberRole.Administrator;

        group.Raise(new GroupMemberRoleChanged(
            group.Id,
            currentOwnerUserId,
            GroupMemberRole.Owner,
            GroupMemberRole.Administrator));

        group.Raise(new GroupMemberRoleChanged(
            group.Id,
            newOwnerUserId,
            previousNewOwnerRole,
            GroupMemberRole.Owner));

        return Result.Success();
    }

    private static GroupJoinApplication CreatePendingApplication(
        Guid groupId,
        Guid userId,
        DateTime submittedAt) =>
        new()
        {
            GroupId = groupId,
            UserId = userId,
            Status = GroupJoinApplicationStatus.Pending,
            SubmittedAt = submittedAt
        };

    private static Result ApproveApplication(
        GroupJoinApplication application,
        DateTime decidedAt,
        Guid decidedByUserId)
    {
        if (application.Status != GroupJoinApplicationStatus.Pending)
        {
            return Result.Failure(GroupJoinApplicationErrors.NotPending);
        }

        application.Status = GroupJoinApplicationStatus.Approved;
        application.DecidedAt = decidedAt;
        application.DecidedByUserId = decidedByUserId;

        return Result.Success();
    }

    private static Result RejectApplication(
        GroupJoinApplication application,
        DateTime decidedAt,
        Guid decidedByUserId)
    {
        if (application.Status != GroupJoinApplicationStatus.Pending)
        {
            return Result.Failure(GroupJoinApplicationErrors.NotPending);
        }

        application.Status = GroupJoinApplicationStatus.Rejected;
        application.DecidedAt = decidedAt;
        application.DecidedByUserId = decidedByUserId;

        return Result.Success();
    }

    private static Result EnsureNotDeleted(Group group) =>
        group.IsDeleted ? Result.Failure(GroupErrors.Deleted(group.Id)) : Result.Success();

    private static bool IsMember(Group group, Guid userId) => GetMembership(group, userId) is not null;

    private static GroupMembership? GetMembership(Group group, Guid userId) =>
        group.GroupMemberships.FirstOrDefault(m => m.UserId == userId);

    private static bool HasPendingApplication(Group group, Guid userId) =>
        group.JoinApplications.Any(a =>
            a.UserId == userId && a.Status == GroupJoinApplicationStatus.Pending);

    private static GroupJoinApplication? FindApplication(Group group, Guid applicationId) =>
        group.JoinApplications.FirstOrDefault(a => a.Id == applicationId);

    private static Result ValidateReapplyCooldown(Group group, Guid userId, DateTime utcNow)
    {
        var lastRejected = group.JoinApplications
            .Where(a => a.UserId == userId && a.Status == GroupJoinApplicationStatus.Rejected)
            .OrderByDescending(a => a.DecidedAt)
            .FirstOrDefault();

        if (lastRejected?.DecidedAt is null)
        {
            return Result.Success();
        }

        if (lastRejected.DecidedAt.Value.Add(GroupConstants.ReapplyCooldown) > utcNow)
        {
            return Result.Failure(GroupErrors.ReapplyCooldownActive);
        }

        return Result.Success();
    }

    private static Result<string> ValidateName(string name)
    {
        var trimmed = name.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return Result.Failure<string>(GroupErrors.InvalidName);
        }

        if (trimmed.Length > GroupConstants.NameMaxLength)
        {
            return Result.Failure<string>(GroupErrors.NameTooLong(GroupConstants.NameMaxLength));
        }

        return trimmed;
    }

    private static string NormalizeDescription(string? description) =>
        string.IsNullOrEmpty(description) ? string.Empty : description;
}
