using Domain.Groups.Events;
using Domain.Participants;
using SharedKernel;

namespace Domain.Groups;

public class Group : Participant
{
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string ProfileImageUrl { get; private set; } = string.Empty;
    public GroupJoinPolicy JoinPolicy { get; private set; } = GroupJoinPolicy.Open;
    public DateTime? DeletedAt { get; private set; }

    public bool IsDeleted => DeletedAt is not null;

    public List<GroupMembership> GroupMemberships { get; private set; } = [];
    public List<GroupJoinApplication> JoinApplications { get; private set; } = [];

    private Group()
    {
    }

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

    public Result UpdateProfile(string name, string? description, string? profileImageUrl)
    {
        var deletedResult = EnsureNotDeleted();
        if (deletedResult.IsFailure)
        {
            return deletedResult;
        }

        var nameResult = ValidateName(name);
        if (nameResult.IsFailure)
        {
            return nameResult;
        }

        Name = nameResult.Value;
        Description = NormalizeDescription(description);

        if (profileImageUrl is not null)
        {
            ProfileImageUrl = profileImageUrl;
        }

        Raise(new GroupProfileUpdated(Id));
        return Result.Success();
    }

    public Result ChangeJoinPolicy(GroupJoinPolicy newPolicy)
    {
        var deletedResult = EnsureNotDeleted();
        if (deletedResult.IsFailure)
        {
            return deletedResult;
        }

        if (JoinPolicy == newPolicy)
        {
            return Result.Success();
        }

        JoinPolicy = newPolicy;
        JoinApplications.Clear();

        Raise(new GroupJoinPolicyChanged(Id, newPolicy));
        return Result.Success();
    }

    public Result SoftDelete()
    {
        if (IsDeleted)
        {
            return Result.Success();
        }

        DeletedAt = DateTime.UtcNow;
        JoinApplications.Clear();

        Raise(new GroupSoftDeleted(Id));
        return Result.Success();
    }

    public Result<GroupMembership> JoinOpen(Guid userId)
    {
        var deletedResult = EnsureNotDeleted();
        if (deletedResult.IsFailure)
        {
            return Result.Failure<GroupMembership>(deletedResult.Error);
        }

        if (JoinPolicy != GroupJoinPolicy.Open)
        {
            return Result.Failure<GroupMembership>(GroupErrors.NotOpenForJoin);
        }

        if (IsMember(userId))
        {
            return Result.Failure<GroupMembership>(GroupErrors.AlreadyMember);
        }

        var membership = GroupMembership.Create(Id, userId, GroupMemberRole.Member);
        GroupMemberships.Add(membership);

        Raise(new GroupMemberJoined(Id, userId, GroupMemberRole.Member));
        return membership;
    }

    public Result<GroupJoinApplication> SubmitJoinApplication(Guid userId, DateTime utcNow)
    {
        var deletedResult = EnsureNotDeleted();
        if (deletedResult.IsFailure)
        {
            return Result.Failure<GroupJoinApplication>(deletedResult.Error);
        }

        if (JoinPolicy != GroupJoinPolicy.ApplicationRequired)
        {
            return Result.Failure<GroupJoinApplication>(GroupErrors.ApplicationsNotRequired);
        }

        if (IsMember(userId))
        {
            return Result.Failure<GroupJoinApplication>(GroupErrors.AlreadyMember);
        }

        if (HasPendingApplication(userId))
        {
            return Result.Failure<GroupJoinApplication>(GroupErrors.PendingApplicationExists);
        }

        var cooldownResult = ValidateReapplyCooldown(userId, utcNow);
        if (cooldownResult.IsFailure)
        {
            return Result.Failure<GroupJoinApplication>(cooldownResult.Error);
        }

        var application = GroupJoinApplication.CreatePending(Id, userId, utcNow);
        JoinApplications.Add(application);

        Raise(new GroupJoinApplicationSubmitted(Id, application.Id, userId));
        return application;
    }

    public Result<GroupMembership> ApproveApplication(
        Guid applicationId,
        Guid decidedByUserId,
        DateTime utcNow)
    {
        var deletedResult = EnsureNotDeleted();
        if (deletedResult.IsFailure)
        {
            return Result.Failure<GroupMembership>(deletedResult.Error);
        }

        var application = FindApplication(applicationId);
        if (application is null)
        {
            return Result.Failure<GroupMembership>(GroupJoinApplicationErrors.NotFound(applicationId));
        }

        var approveResult = application.Approve(utcNow, decidedByUserId);
        if (approveResult.IsFailure)
        {
            return Result.Failure<GroupMembership>(approveResult.Error);
        }

        if (IsMember(application.UserId))
        {
            return Result.Failure<GroupMembership>(GroupErrors.AlreadyMember);
        }

        var membership = GroupMembership.Create(Id, application.UserId, GroupMemberRole.Member);
        GroupMemberships.Add(membership);

        Raise(new GroupJoinApplicationApproved(Id, application.Id, application.UserId));
        Raise(new GroupMemberJoined(Id, application.UserId, GroupMemberRole.Member));
        return membership;
    }

    public Result RejectApplication(Guid applicationId, Guid decidedByUserId, DateTime utcNow)
    {
        var deletedResult = EnsureNotDeleted();
        if (deletedResult.IsFailure)
        {
            return deletedResult;
        }

        var application = FindApplication(applicationId);
        if (application is null)
        {
            return Result.Failure(GroupJoinApplicationErrors.NotFound(applicationId));
        }

        var rejectResult = application.Reject(utcNow, decidedByUserId);
        if (rejectResult.IsFailure)
        {
            return rejectResult;
        }

        Raise(new GroupJoinApplicationRejected(Id, application.Id, application.UserId));
        return Result.Success();
    }

    public Result CancelApplication(Guid applicationId, Guid userId)
    {
        var deletedResult = EnsureNotDeleted();
        if (deletedResult.IsFailure)
        {
            return deletedResult;
        }

        var application = FindApplication(applicationId);
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

        Raise(new GroupJoinApplicationCancelled(Id, application.Id, userId));
        JoinApplications.Remove(application);
        return Result.Success();
    }

    public Result TransferOwnership(Guid currentOwnerUserId, Guid newOwnerUserId)
    {
        var deletedResult = EnsureNotDeleted();
        if (deletedResult.IsFailure)
        {
            return deletedResult;
        }

        if (currentOwnerUserId == newOwnerUserId)
        {
            return Result.Failure(GroupErrors.CannotTransferOwnershipToSelf);
        }

        var currentOwnerMembership = GetMembership(currentOwnerUserId);
        if (currentOwnerMembership is null || currentOwnerMembership.Role != GroupMemberRole.Owner)
        {
            return Result.Failure(GroupErrors.NotOwner);
        }

        var newOwnerMembership = GetMembership(newOwnerUserId);
        if (newOwnerMembership is null)
        {
            return Result.Failure(GroupErrors.CannotTransferOwnershipToNonMember);
        }

        var previousNewOwnerRole = newOwnerMembership.Role;
        newOwnerMembership.Role = GroupMemberRole.Owner;
        currentOwnerMembership.Role = GroupMemberRole.Administrator;

        Raise(new GroupMemberRoleChanged(
            Id,
            currentOwnerUserId,
            GroupMemberRole.Owner,
            GroupMemberRole.Administrator));

        Raise(new GroupMemberRoleChanged(
            Id,
            newOwnerUserId,
            previousNewOwnerRole,
            GroupMemberRole.Owner));

        return Result.Success();
    }

    private Result EnsureNotDeleted() =>
        IsDeleted ? Result.Failure(GroupErrors.Deleted(Id)) : Result.Success();

    private bool IsMember(Guid userId) => GetMembership(userId) is not null;

    private GroupMembership? GetMembership(Guid userId) =>
        GroupMemberships.FirstOrDefault(m => m.UserId == userId);

    private bool HasPendingApplication(Guid userId) =>
        JoinApplications.Any(a =>
            a.UserId == userId && a.Status == GroupJoinApplicationStatus.Pending);

    private GroupJoinApplication? FindApplication(Guid applicationId) =>
        JoinApplications.FirstOrDefault(a => a.Id == applicationId);

    private Result ValidateReapplyCooldown(Guid userId, DateTime utcNow)
    {
        var lastRejected = JoinApplications
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
