using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Media;
using Application.Abstractions.Messaging;
using Application.Groups.CreateGroup;
using Application.Groups.Services;
using Application.Users.Services;
using Domain.Groups;
using Domain.Groups.Services;
using SharedKernel;

namespace Application.Groups.UploadGroupProfileImage;

internal sealed class UploadGroupProfileImageCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IUserIdentityAccessor identityAccessor,
    GroupAccessService groupAccessService,
    IMediaStorageService mediaStorageService)
    : ICommandHandler<UploadGroupProfileImageCommand, GroupResponse>
{
    public async Task<Result<GroupResponse>> Handle(
        UploadGroupProfileImageCommand command,
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

        if (!GroupPermissions.CanEditProfile(membershipResult.Value.Role))
        {
            return Result.Failure<GroupResponse>(GroupErrors.InsufficientPermissions());
        }

        var previousImageUrl = group.ProfileImageUrl;

        var saveResult = await mediaStorageService.SaveAsync(
            MediaPurpose.GroupProfile,
            command.GroupId,
            command.Upload,
            cancellationToken);

        if (saveResult.IsFailure)
        {
            return Result.Failure<GroupResponse>(saveResult.Error);
        }

        var updateResult = GroupService.UpdateProfile(
            group,
            group.Name,
            group.Description,
            saveResult.Value);

        if (updateResult.IsFailure)
        {
            await mediaStorageService.TryDeleteLocalFileAsync(saveResult.Value, cancellationToken);
            return Result.Failure<GroupResponse>(updateResult.Error);
        }

        await context.SaveChangesAsync(cancellationToken);

        await mediaStorageService.TryDeleteLocalFileAsync(previousImageUrl, cancellationToken);

        return new GroupResponse(
            group.Id,
            group.Name,
            group.Description,
            group.JoinPolicy,
            group.ProfileImageUrl,
            group.CreatedAt);
    }
}
