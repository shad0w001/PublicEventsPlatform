using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Events.Services;
using Application.Users.Services;
using Domain.Events.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace Application.Events.PublishEvent;

internal sealed class PublishEventCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IUserIdentityAccessor identityAccessor,
    EventAccessService eventAccessService,
    IOptions<EventOptions> eventOptions)
    : ICommandHandler<PublishEventCommand, EventDetailResponse>
{
    public async Task<Result<EventDetailResponse>> Handle(
        PublishEventCommand command,
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

        var recentPublishCount = await eventAccessService.CountRecentPublishesByHostAsync(
            editAccess.HostParticipantId,
            cancellationToken);

        var categoryExists = @event.CategoryId is not null &&
                             await context.EventCategories
                                 .AsNoTracking()
                                 .AnyAsync(c => c.Id == @event.CategoryId.Value, cancellationToken);

        var publishResult = EventService.Publish(
            @event,
            categoryExists,
            recentPublishCount,
            eventOptions.Value.MaxPublishesPerHostPerWeek);

        if (publishResult.IsFailure)
        {
            return Result.Failure<EventDetailResponse>(publishResult.Error);
        }

        await context.SaveChangesAsync(cancellationToken);

        return EventMapping.ToDetailResponse(@event, editAccess);
    }
}
