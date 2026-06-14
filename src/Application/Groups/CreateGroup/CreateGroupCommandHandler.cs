using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Groups.Services;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace Application.Groups.CreateGroup;

internal sealed class CreateGroupCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IUserIdentityAccessor identityAccessor,
    IOptions<GroupProfileOptions> groupProfileOptions)
    : ICommandHandler<CreateGroupCommand, GroupResponse>
{
    public async Task<Result<GroupResponse>> Handle(
        CreateGroupCommand command,
        CancellationToken cancellationToken)
    {
        var gateResult = VerifiedUserGate.EnsureVerified(identityAccessor);
        if (gateResult.IsFailure)
        {
            return Result.Failure<GroupResponse>(gateResult.Error);
        }

        var userResult = await currentUserService.GetOrProvisionAsync(cancellationToken);
        if (userResult.IsFailure)
        {
            return Result.Failure<GroupResponse>(userResult.Error);
        }

        var createResult = GroupService.Create(
            command.Name,
            command.Description,
            command.JoinPolicy,
            userResult.Value.Id,
            groupProfileOptions.Value.DefaultImageUrl,
            command.ProfileImageUrl);

        if (createResult.IsFailure)
        {
            return Result.Failure<GroupResponse>(createResult.Error);
        }

        var group = createResult.Value.Group;
        var ownerMembership = createResult.Value.OwnerMembership;

        context.Groups.Add(group);
        context.GroupMemberships.Add(ownerMembership);
        await context.SaveChangesAsync(cancellationToken);

        return MapToResponse(group);
    }

    private static GroupResponse MapToResponse(Domain.Groups.Group group) =>
        new(
            group.Id,
            group.Name,
            group.Description,
            group.JoinPolicy,
            group.ProfileImageUrl,
            group.CreatedAt);
}
