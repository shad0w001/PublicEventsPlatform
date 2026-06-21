using Domain.Groups.Events;
using SharedKernel;

namespace Domain.Groups.Services;

public static class GroupVerificationService
{
    public static Result<GroupVerificationApplication> SubmitApplication(
        Group group,
        Guid submittedByUserId,
        int verifiedMemberCount,
        int completedEventCount,
        DateTime utcNow)
    {
        var deletedResult = EnsureNotDeleted(group);
        if (deletedResult.IsFailure)
        {
            return Result.Failure<GroupVerificationApplication>(deletedResult.Error);
        }

        if (group.IsVerified)
        {
            return Result.Failure<GroupVerificationApplication>(GroupErrors.AlreadyVerified);
        }

        if (HasPendingVerificationApplication(group))
        {
            return Result.Failure<GroupVerificationApplication>(GroupErrors.VerificationPendingApplicationExists);
        }

        var cooldownResult = ValidateReapplyCooldown(group, utcNow);
        if (cooldownResult.IsFailure)
        {
            return Result.Failure<GroupVerificationApplication>(cooldownResult.Error);
        }

        var eligibilityResult = ValidateEligibility(verifiedMemberCount, completedEventCount);
        if (eligibilityResult.IsFailure)
        {
            return Result.Failure<GroupVerificationApplication>(eligibilityResult.Error);
        }

        var application = CreatePendingApplication(group.Id, submittedByUserId, utcNow);
        group.VerificationApplications.Add(application);

        return application;
    }

    public static Result ApproveApplication(
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

        if (group.IsVerified)
        {
            return Result.Failure(GroupErrors.AlreadyVerified);
        }

        var application = FindApplication(group, applicationId);
        if (application is null)
        {
            return Result.Failure(GroupVerificationApplicationErrors.NotFound(applicationId));
        }

        var approveResult = ApproveApplication(application, utcNow, decidedByUserId);
        if (approveResult.IsFailure)
        {
            return approveResult;
        }

        group.IsVerified = true;
        group.VerifiedAt = utcNow;

        group.Raise(new GroupVerificationApplicationApproved(group.Id, application.Id));
        return Result.Success();
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
            return Result.Failure(GroupVerificationApplicationErrors.NotFound(applicationId));
        }

        var rejectResult = RejectApplication(application, utcNow, decidedByUserId);
        if (rejectResult.IsFailure)
        {
            return rejectResult;
        }

        group.Raise(new GroupVerificationApplicationRejected(group.Id, application.Id));
        return Result.Success();
    }

    private static Result ValidateEligibility(int verifiedMemberCount, int completedEventCount)
    {
        if (verifiedMemberCount < GroupVerificationConstants.MinVerifiedMembers)
        {
            return Result.Failure(GroupErrors.InsufficientVerifiedMembers);
        }

        if (completedEventCount < GroupVerificationConstants.MinCompletedEvents)
        {
            return Result.Failure(GroupErrors.InsufficientCompletedEvents);
        }

        return Result.Success();
    }

    private static GroupVerificationApplication CreatePendingApplication(
        Guid groupId,
        Guid submittedByUserId,
        DateTime submittedAt) =>
        new()
        {
            GroupId = groupId,
            SubmittedByUserId = submittedByUserId,
            Status = GroupVerificationApplicationStatus.Pending,
            SubmittedAt = submittedAt
        };

    private static Result ApproveApplication(
        GroupVerificationApplication application,
        DateTime decidedAt,
        Guid decidedByUserId)
    {
        if (application.Status != GroupVerificationApplicationStatus.Pending)
        {
            return Result.Failure(GroupVerificationApplicationErrors.NotPending);
        }

        application.Status = GroupVerificationApplicationStatus.Approved;
        application.DecidedAt = decidedAt;
        application.DecidedByUserId = decidedByUserId;

        return Result.Success();
    }

    private static Result RejectApplication(
        GroupVerificationApplication application,
        DateTime decidedAt,
        Guid decidedByUserId)
    {
        if (application.Status != GroupVerificationApplicationStatus.Pending)
        {
            return Result.Failure(GroupVerificationApplicationErrors.NotPending);
        }

        application.Status = GroupVerificationApplicationStatus.Rejected;
        application.DecidedAt = decidedAt;
        application.DecidedByUserId = decidedByUserId;

        return Result.Success();
    }

    private static Result EnsureNotDeleted(Group group) =>
        group.IsDeleted ? Result.Failure(GroupErrors.Deleted(group.Id)) : Result.Success();

    private static bool HasPendingVerificationApplication(Group group) =>
        group.VerificationApplications.Any(a => a.Status == GroupVerificationApplicationStatus.Pending);

    private static GroupVerificationApplication? FindApplication(Group group, Guid applicationId) =>
        group.VerificationApplications.FirstOrDefault(a => a.Id == applicationId);

    private static Result ValidateReapplyCooldown(Group group, DateTime utcNow)
    {
        var lastRejected = group.VerificationApplications
            .Where(a => a.Status == GroupVerificationApplicationStatus.Rejected)
            .OrderByDescending(a => a.DecidedAt)
            .FirstOrDefault();

        if (lastRejected?.DecidedAt is null)
        {
            return Result.Success();
        }

        if (lastRejected.DecidedAt.Value.Add(GroupConstants.ReapplyCooldown) > utcNow)
        {
            return Result.Failure(GroupErrors.VerificationReapplyCooldownActive);
        }

        return Result.Success();
    }
}
