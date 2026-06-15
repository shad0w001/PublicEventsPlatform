using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Events.Services;
using Application.Users.Services;
using Domain.Events;
using Domain.Events.Services;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Events.UpdateEvent;

internal sealed class UpdateEventCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IUserIdentityAccessor identityAccessor,
    EventAccessService eventAccessService,
    EventVenueConflictService venueConflictService)
    : ICommandHandler<UpdateEventCommand, EventDetailResponse>
{
    public async Task<Result<EventDetailResponse>> Handle(
        UpdateEventCommand command,
        CancellationToken cancellationToken)
    {
        var gateResult = VerifiedUserGate.EnsureVerified(identityAccessor);
        if (gateResult.IsFailure)
        {
            return Result.Failure<EventDetailResponse>(gateResult.Error);
        }

        if (!HasAnyField(command))
        {
            return Result.Failure<EventDetailResponse>(EventErrors.NoFieldsToUpdate);
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

        if (command.CategoryId is not null)
        {
            var categoryExists = await context.EventCategories
                .AsNoTracking()
                .AnyAsync(c => c.Id == command.CategoryId.Value, cancellationToken);

            if (!categoryExists)
            {
                return Result.Failure<EventDetailResponse>(
                    EventErrors.CategoryNotFound(command.CategoryId.Value));
            }
        }

        var patch = new EventUpdatePatch
        {
            Title = command.Title,
            Description = command.Description,
            BannerImageUrl = command.BannerImageUrl,
            CategoryId = command.CategoryId,
            StartTime = command.StartTime,
            EndTime = command.EndTime,
            Tier = command.Tier,
            TimeZoneId = command.TimeZoneId,
            AdmissionType = command.AdmissionType,
            Locations = command.Locations?.Select(EventMapping.ToDomainLocation).ToList()
        };

        var updateResult = EventService.Update(@event, patch, user.Id);
        if (updateResult.IsFailure)
        {
            return Result.Failure<EventDetailResponse>(updateResult.Error);
        }

        if (ShouldCheckVenueConflict(@event, command))
        {
            var venueResult = await venueConflictService.EnsureNoConflictAsync(@event, cancellationToken);
            if (venueResult.IsFailure)
            {
                return Result.Failure<EventDetailResponse>(venueResult.Error);
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        var categoryName = await EventCategoryLookup.ResolveNameAsync(
            context,
            @event.CategoryId,
            cancellationToken);

        return EventMapping.ToDetailResponse(@event, editAccess, categoryName);
    }

    private static bool ShouldCheckVenueConflict(Event @event, UpdateEventCommand command) =>
        @event.Status == EventStatus.Published &&
        (command.Locations is not null ||
         command.StartTime is not null ||
         command.EndTime is not null);

    private static bool HasAnyField(UpdateEventCommand command) =>
        command.Title is not null ||
        command.Description is not null ||
        command.CategoryId is not null ||
        command.BannerImageUrl is not null ||
        command.Tier is not null ||
        command.Locations is not null ||
        command.StartTime is not null ||
        command.EndTime is not null ||
        command.TimeZoneId is not null ||
        command.AdmissionType is not null;
}
