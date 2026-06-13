using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Groups.CreateGroup;
using Application.Groups.Services;
using Application.Users.Services;
using Domain.Groups;
using Domain.Groups.Services;
using SharedKernel;

namespace Application.Groups.UpdateGroup;

internal sealed class UpdateGroupCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IUserIdentityAccessor identityAccessor,
    GroupAccessService groupAccessService)
    : ICommandHandler<UpdateGroupCommand, GroupResponse>
{
    public async Task<Result<GroupResponse>> Handle(
        UpdateGroupCommand command,
        CancellationToken cancellationToken)
    {
        var gateResult = VerifiedUserGate.EnsureVerified(identityAccessor);
        if (gateResult.IsFailure)
        {
            return Result.Failure<GroupResponse>(gateResult.Error);
        }

        if (command.Name is null &&
            command.Description is null &&
            command.ProfileImageUrl is null &&
            command.JoinPolicy is null)
        {
            return Result.Failure<GroupResponse>(GroupErrors.NoFieldsToUpdate);
        }

        var userResult = await currentUserService.GetOrProvisionAsync(cancellationToken);
        if (userResult.IsFailure)
        {
            return Result.Failure<GroupResponse>(userResult.Error);
        }

        var groupResult = await groupAccessService.GetActiveGroupAsync(command.GroupId, cancellationToken);
        if (groupResult.IsFailure)
        {
            return Result.Failure<GroupResponse>(groupResult.Error);
        }

        var group = groupResult.Value;

        var membershipResult = groupAccessService.GetMembership(group, userResult.Value.Id);
        if (membershipResult.IsFailure)
        {
            return Result.Failure<GroupResponse>(membershipResult.Error);
        }

        var actorRole = membershipResult.Value.Role;

        var hasProfileUpdate = command.Name is not null ||
                               command.Description is not null ||
                               command.ProfileImageUrl is not null;

        if (hasProfileUpdate)
        {
            if (!GroupPermissions.CanEditProfile(actorRole))
            {
                return Result.Failure<GroupResponse>(GroupErrors.InsufficientPermissions());
            }

            var updateResult = GroupService.UpdateProfile(
                group,
                command.Name ?? group.Name,
                command.Description ?? group.Description,
                command.ProfileImageUrl);

            if (updateResult.IsFailure)
            {
                return Result.Failure<GroupResponse>(updateResult.Error);
            }
        }

        if (command.JoinPolicy is not null)
        {
            if (!GroupPermissions.CanChangeJoinPolicy(actorRole))
            {
                return Result.Failure<GroupResponse>(GroupErrors.InsufficientPermissions());
            }

            var policyResult = GroupService.ChangeJoinPolicy(group, command.JoinPolicy.Value);
            if (policyResult.IsFailure)
            {
                return Result.Failure<GroupResponse>(policyResult.Error);
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        return new GroupResponse(
            group.Id,
            group.Name,
            group.Description,
            group.JoinPolicy,
            group.ProfileImageUrl,
            group.CreatedAt);
    }
}
