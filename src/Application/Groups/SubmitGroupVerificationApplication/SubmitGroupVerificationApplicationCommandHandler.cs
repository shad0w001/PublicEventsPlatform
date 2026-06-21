using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Groups.Services;
using Application.Users.Services;
using Domain.Groups;
using Domain.Groups.Services;
using SharedKernel;

namespace Application.Groups.SubmitGroupVerificationApplication;

internal sealed class SubmitGroupVerificationApplicationCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IUserIdentityAccessor identityAccessor,
    GroupAccessService groupAccessService,
    GroupVerificationEligibilityService eligibilityService)
    : ICommandHandler<SubmitGroupVerificationApplicationCommand, GroupVerificationApplicationResponse>
{
    public async Task<Result<GroupVerificationApplicationResponse>> Handle(
        SubmitGroupVerificationApplicationCommand command,
        CancellationToken cancellationToken)
    {
        var gateResult = VerifiedUserGate.EnsureVerified(identityAccessor);
        if (gateResult.IsFailure)
        {
            return Result.Failure<GroupVerificationApplicationResponse>(gateResult.Error);
        }

        var userResult = await currentUserService.GetOrProvisionAsync(cancellationToken);
        if (userResult.IsFailure)
        {
            return Result.Failure<GroupVerificationApplicationResponse>(userResult.Error);
        }

        var groupResult = await groupAccessService.GetActiveGroupForVerificationAsync(
            command.GroupId,
            cancellationToken);
        if (groupResult.IsFailure)
        {
            return Result.Failure<GroupVerificationApplicationResponse>(groupResult.Error);
        }

        var group = groupResult.Value;
        var membershipResult = groupAccessService.GetMembership(group, userResult.Value.Id);
        if (membershipResult.IsFailure)
        {
            return Result.Failure<GroupVerificationApplicationResponse>(GroupErrors.InsufficientPermissions());
        }

        if (!GroupPermissions.CanSubmitVerificationApplication(membershipResult.Value.Role))
        {
            return Result.Failure<GroupVerificationApplicationResponse>(GroupErrors.InsufficientPermissions());
        }

        var utcNow = DateTime.UtcNow;
        var snapshot = await eligibilityService.GetSnapshotAsync(group.Id, utcNow, cancellationToken);

        var submitResult = GroupVerificationService.SubmitApplication(
            group,
            userResult.Value.Id,
            snapshot.VerifiedMemberCount,
            snapshot.CompletedEventCount,
            utcNow);

        if (submitResult.IsFailure)
        {
            return Result.Failure<GroupVerificationApplicationResponse>(submitResult.Error);
        }

        var application = submitResult.Value;
        context.GroupVerificationApplications.Add(application);
        await context.SaveChangesAsync(cancellationToken);

        return GroupVerificationApplicationMapper.ToResponse(application, group.Name);
    }
}
