using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Media;
using Application.Abstractions.Messaging;
using Application.Events.Services;
using Application.Users.Services;
using Domain.Events;
using Domain.Events.Services;
using SharedKernel;

namespace Application.Events.UploadEventBanner;

internal sealed class UploadEventBannerCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IUserIdentityAccessor identityAccessor,
    EventAccessService eventAccessService,
    IMediaStorageService mediaStorageService)
    : ICommandHandler<UploadEventBannerCommand, EventDetailResponse>
{
    public async Task<Result<EventDetailResponse>> Handle(
        UploadEventBannerCommand command,
        CancellationToken cancellationToken)
    {
        var gateResult = VerifiedUserGate.EnsureVerified(identityAccessor);
        if (gateResult.IsFailure)
        {
            return Result.Failure<EventDetailResponse>(gateResult.Error);
        }

        var userResult = await currentUserService.GetOrProvisionAsync(cancellationToken);
        if (userResult.IsFailure)
        {
            return Result.Failure<EventDetailResponse>(userResult.Error);
        }

        var user = userResult.Value;

        var eventResult = await eventAccessService.GetActiveEventAsync(command.EventId, cancellationToken);
        if (eventResult.IsFailure)
        {
            return Result.Failure<EventDetailResponse>(eventResult.Error);
        }

        var @event = eventResult.Value;

        var editAccessResult = await eventAccessService.ResolveEditAccessAsync(
            @event,
            user.Id,
            user.Id,
            cancellationToken);

        if (editAccessResult.IsFailure)
        {
            return Result.Failure<EventDetailResponse>(editAccessResult.Error);
        }

        var editAccess = editAccessResult.Value;
        var previousBannerUrl = @event.BannerImageUrl;

        var saveResult = await mediaStorageService.SaveAsync(
            MediaPurpose.EventBanner,
            command.EventId,
            command.Upload,
            cancellationToken);

        if (saveResult.IsFailure)
        {
            return Result.Failure<EventDetailResponse>(saveResult.Error);
        }

        var updateResult = EventService.Update(
            @event,
            new EventUpdatePatch { BannerImageUrl = saveResult.Value },
            user.Id);

        if (updateResult.IsFailure)
        {
            await mediaStorageService.TryDeleteLocalFileAsync(saveResult.Value, cancellationToken);
            return Result.Failure<EventDetailResponse>(updateResult.Error);
        }

        await context.SaveChangesAsync(cancellationToken);

        await mediaStorageService.TryDeleteLocalFileAsync(previousBannerUrl, cancellationToken);

        var categoryName = await EventCategoryLookup.ResolveNameAsync(
            context,
            @event.CategoryId,
            cancellationToken);

        return EventMapping.ToDetailResponse(@event, editAccess, categoryName);
    }
}
