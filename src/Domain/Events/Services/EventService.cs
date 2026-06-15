using Domain.Events.EventLocations;
using Domain.Events.Events;
using SharedKernel;

namespace Domain.Events.Services;

public static class EventService
{
    public static Result<(Event Event, EventOrganizer Organizer)> Create(
        EventTier tier,
        string title,
        Guid hostParticipantId)
    {
        var titleResult = ValidateTitle(title, required: true);
        if (titleResult.IsFailure)
        {
            return Result.Failure<(Event, EventOrganizer)>(titleResult.Error);
        }

        var trimmedTitle = title.Trim();

        var @event = new Event
        {
            Tier = tier,
            Title = trimmedTitle,
            Description = string.Empty,
            StartTime = EventConstants.DraftEpochUtc,
            EndTime = EventConstants.DraftEpochUtc,
            Status = EventStatus.Draft,
            LocationType = EventLocationType.Physical
        };

        var organizer = EventOrganizer.Create(@event.Id, hostParticipantId);
        @event.Organizers.Add(organizer);

        @event.Raise(new EventCreated(@event.Id, hostParticipantId, tier));
        return (@event, organizer);
    }

    public static Result Update(Event @event, EventUpdatePatch patch, Guid actingUserId)
    {
        var guardResult = EnsureModifiable(@event);
        if (guardResult.IsFailure)
        {
            return guardResult;
        }

        if (!patch.HasAnyField)
        {
            return Result.Failure(EventErrors.NoFieldsToUpdate);
        }

        if (@event.CreatedByUserId is null)
        {
            @event.CreatedByUserId = actingUserId;
        }

        if (patch.Title is not null)
        {
            var titleResult = ValidateTitle(patch.Title, required: true);
            if (titleResult.IsFailure)
            {
                return titleResult;
            }

            @event.Title = patch.Title.Trim();
        }

        if (patch.Description is not null)
        {
            var descriptionResult = ValidateDescription(patch.Description, required: false);
            if (descriptionResult.IsFailure)
            {
                return descriptionResult;
            }

            @event.Description = patch.Description.Trim();
        }

        if (patch.BannerImageUrl is not null)
        {
            @event.BannerImageUrl = patch.BannerImageUrl;
        }

        if (patch.CategoryId is not null)
        {
            @event.CategoryId = patch.CategoryId;
        }

        if (patch.StartTime is not null)
        {
            @event.StartTime = patch.StartTime.Value;
        }

        if (patch.EndTime is not null)
        {
            @event.EndTime = patch.EndTime.Value;
        }

        if (patch.StartTime is not null || patch.EndTime is not null)
        {
            var timeRangeResult = ValidateEffectiveTimeRange(@event.StartTime, @event.EndTime);
            if (timeRangeResult.IsFailure)
            {
                return timeRangeResult;
            }

            var segmentsResult = ValidateAllLocationSegments(@event);
            if (segmentsResult.IsFailure)
            {
                return segmentsResult;
            }
        }

        if (patch.TimeZoneId is not null)
        {
            if (!IsValidTimeZoneId(patch.TimeZoneId))
            {
                return Result.Failure(EventErrors.InvalidTimeZoneId);
            }

            @event.TimeZoneId = patch.TimeZoneId;
        }

        if (patch.AdmissionType is not null)
        {
            @event.AdmissionType = patch.AdmissionType;
        }

        if (patch.Tier is not null)
        {
            var tierResult = ApplyTierChange(@event, patch.Tier.Value);
            if (tierResult.IsFailure)
            {
                return tierResult;
            }
        }

        if (patch.Locations is not null)
        {
            @event.Locations.Clear();
            foreach (var location in patch.Locations)
            {
                @event.Locations.Add(CloneLocation(location));
            }

            if (@event.Tier == EventTier.Small &&
                @event.Locations.Count > EventConstants.SmallTierMaxLocations)
            {
                return Result.Failure(EventErrors.TooManyLocationsForSmallTier);
            }

            foreach (var location in @event.Locations)
            {
                var segmentResult = ValidateLocationSegment(location, @event.StartTime, @event.EndTime);
                if (segmentResult.IsFailure)
                {
                    return segmentResult;
                }
            }

            @event.LocationType = DeriveLocationType(@event.Locations);
        }

        @event.Raise(new EventUpdated(@event.Id));
        return Result.Success();
    }

    public static Result Publish(Event @event, bool categoryExists, int recentPublishCount, int maxPublishesPerWeek)
    {
        var deletedResult = EnsureNotDeleted(@event);
        if (deletedResult.IsFailure)
        {
            return deletedResult;
        }

        if (@event.Status == EventStatus.Cancelled)
        {
            return Result.Failure(EventErrors.CannotModifyCancelled);
        }

        if (@event.Status == EventStatus.Published)
        {
            return Result.Failure(EventErrors.AlreadyPublished);
        }

        if (@event.Status != EventStatus.Draft)
        {
            return Result.Failure(EventErrors.InvalidStatusTransition);
        }

        var rateLimitResult = ValidatePublishRateLimit(recentPublishCount, maxPublishesPerWeek);
        if (rateLimitResult.IsFailure)
        {
            return rateLimitResult;
        }

        var validationResult = ValidatePublishReady(@event, categoryExists);
        if (validationResult.IsFailure)
        {
            return validationResult;
        }

        @event.LocationType = DeriveLocationType(@event.Locations);
        @event.Status = EventStatus.Published;
        @event.PublishedAt = DateTime.UtcNow;

        var hostParticipantId = @event.Organizers.Single().ParticipantId;
        @event.Raise(new EventPublished(@event.Id, hostParticipantId));
        return Result.Success();
    }

    public static Result Cancel(Event @event)
    {
        var deletedResult = EnsureNotDeleted(@event);
        if (deletedResult.IsFailure)
        {
            return deletedResult;
        }

        if (@event.Status != EventStatus.Published)
        {
            return Result.Failure(EventErrors.NotPublished);
        }

        @event.Status = EventStatus.Cancelled;
        @event.Raise(new EventCancelled(@event.Id));
        return Result.Success();
    }

    public static Result SoftDelete(Event @event)
    {
        if (@event.IsDeleted)
        {
            return Result.Success();
        }

        if (@event.Status != EventStatus.Draft)
        {
            return Result.Failure(EventErrors.NotDraft);
        }

        @event.DeletedAt = DateTime.UtcNow;
        @event.Raise(new EventSoftDeleted(@event.Id));
        return Result.Success();
    }

    public static Result ValidatePublishRateLimit(int recentPublishCount, int maxPublishesPerWeek)
    {
        if (recentPublishCount >= maxPublishesPerWeek)
        {
            return Result.Failure(EventErrors.PublishRateLimitExceeded(maxPublishesPerWeek));
        }

        return Result.Success();
    }

    internal static Result ValidatePublishReady(Event @event, bool categoryExists)
    {
        var titleResult = ValidateTitle(@event.Title, required: true);
        if (titleResult.IsFailure)
        {
            return titleResult;
        }

        var descriptionResult = ValidateDescription(@event.Description, required: true);
        if (descriptionResult.IsFailure)
        {
            return descriptionResult;
        }

        if (@event.CategoryId is null || !categoryExists)
        {
            return Result.Failure(EventErrors.CategoryRequired);
        }

        if (IsDraftTime(@event.StartTime) || IsDraftTime(@event.EndTime))
        {
            return Result.Failure(EventErrors.DraftTimesNotSet);
        }

        if (@event.StartTime >= @event.EndTime)
        {
            return Result.Failure(EventErrors.InvalidTimeRange);
        }

        if (!IsValidTimeZoneId(@event.TimeZoneId))
        {
            return Result.Failure(EventErrors.InvalidTimeZoneId);
        }

        if (@event.AdmissionType is null)
        {
            return Result.Failure(EventErrors.AdmissionTypeRequired);
        }

        // Phase 6: paid events will require at least one TicketType before publish.

        if (@event.Locations.Count == 0)
        {
            return Result.Failure(EventErrors.LocationsRequired);
        }

        if (@event.Tier == EventTier.Small && @event.Locations.Count > EventConstants.SmallTierMaxLocations)
        {
            return Result.Failure(EventErrors.TooManyLocationsForSmallTier);
        }

        foreach (var location in @event.Locations)
        {
            var segmentResult = ValidateLocationSegment(location, @event.StartTime, @event.EndTime);
            if (segmentResult.IsFailure)
            {
                return segmentResult;
            }
        }

        return Result.Success();
    }

    internal static (DateTime Start, DateTime End) GetEffectiveSegmentWindow(
        EventLocation location,
        Event @event)
    {
        var startsAt = NormalizeSegmentTime(location.StartsAt);
        var endsAt = NormalizeSegmentTime(location.EndsAt);

        if (startsAt is not null && endsAt is not null)
        {
            return (startsAt.Value, endsAt.Value);
        }

        return (@event.StartTime, @event.EndTime);
    }

    private static Result ValidateAllLocationSegments(Event @event)
    {
        foreach (var location in @event.Locations)
        {
            var segmentResult = ValidateLocationSegment(location, @event.StartTime, @event.EndTime);
            if (segmentResult.IsFailure)
            {
                return segmentResult;
            }
        }

        return Result.Success();
    }

    internal static EventLocationType DeriveLocationType(IReadOnlyList<EventLocation> locations)
    {
        var hasPhysical = locations.Any(l => l.Kind == EventLocationKind.Physical);
        var hasVirtual = locations.Any(l => l.Kind == EventLocationKind.Virtual);

        if (hasPhysical && hasVirtual)
        {
            return EventLocationType.Hybrid;
        }

        if (hasVirtual)
        {
            return EventLocationType.Online;
        }

        return EventLocationType.Physical;
    }

    private static Result ApplyTierChange(Event @event, EventTier newTier)
    {
        if (@event.Status == EventStatus.Published && newTier < @event.Tier)
        {
            return Result.Failure(EventErrors.CannotDowngradeTier);
        }

        @event.Tier = newTier;
        return Result.Success();
    }

    private static Result EnsureModifiable(Event @event)
    {
        if (@event.IsDeleted)
        {
            return Result.Failure(EventErrors.CannotModifyDeleted);
        }

        if (@event.Status == EventStatus.Cancelled)
        {
            return Result.Failure(EventErrors.CannotModifyCancelled);
        }

        return Result.Success();
    }

    private static Result EnsureNotDeleted(Event @event) =>
        @event.IsDeleted ? Result.Failure(EventErrors.CannotModifyDeleted) : Result.Success();

    private static Result ValidateTitle(string title, bool required)
    {
        var trimmed = title.Trim();

        if (required && string.IsNullOrEmpty(trimmed))
        {
            return Result.Failure(EventErrors.InvalidTitle);
        }

        if (trimmed.Length > EventConstants.TitleMaxLength)
        {
            return Result.Failure(EventErrors.TitleTooLong(EventConstants.TitleMaxLength));
        }

        return Result.Success();
    }

    private static Result ValidateDescription(string description, bool required)
    {
        var trimmed = description.Trim();

        if (required && string.IsNullOrEmpty(trimmed))
        {
            return Result.Failure(EventErrors.InvalidDescription);
        }

        if (trimmed.Length > EventConstants.DescriptionMaxLength)
        {
            return Result.Failure(EventErrors.DescriptionTooLong(EventConstants.DescriptionMaxLength));
        }

        return Result.Success();
    }

    private static Result ValidateLocationSegment(
        EventLocation location,
        DateTime eventStart,
        DateTime eventEnd)
    {
        if (string.IsNullOrWhiteSpace(location.Name))
        {
            return Result.Failure(EventErrors.InvalidLocationSegment);
        }

        var kindResult = location.Kind switch
        {
            EventLocationKind.Physical when string.IsNullOrWhiteSpace(location.Address) &&
                                            string.IsNullOrWhiteSpace(location.City) &&
                                            !(location.Latitude.HasValue && location.Longitude.HasValue) =>
                Result.Failure(EventErrors.InvalidLocationSegment),
            EventLocationKind.Virtual when string.IsNullOrWhiteSpace(location.Url) =>
                Result.Failure(EventErrors.InvalidLocationSegment),
            _ => Result.Success()
        };

        if (kindResult.IsFailure)
        {
            return kindResult;
        }

        return ValidateSegmentTimes(location, eventStart, eventEnd);
    }

    private static Result ValidateSegmentTimes(
        EventLocation location,
        DateTime eventStart,
        DateTime eventEnd)
    {
        var startsAt = NormalizeSegmentTime(location.StartsAt);
        var endsAt = NormalizeSegmentTime(location.EndsAt);

        if (startsAt is null && endsAt is null)
        {
            return Result.Success();
        }

        if (startsAt is null || endsAt is null)
        {
            return Result.Failure(EventErrors.SegmentTimesIncomplete);
        }

        if (startsAt >= endsAt)
        {
            return Result.Failure(EventErrors.InvalidSegmentTimeRange);
        }

        if (IsDraftTime(eventStart) || IsDraftTime(eventEnd))
        {
            return Result.Success();
        }

        if (startsAt < eventStart || endsAt > eventEnd)
        {
            return Result.Failure(EventErrors.SegmentTimeOutOfBounds);
        }

        return Result.Success();
    }

    private static DateTime? NormalizeSegmentTime(DateTime? time) =>
        time is null || IsDraftTime(time.Value) ? null : time;

    private static bool IsDraftTime(DateTime time) =>
        time == EventConstants.DraftEpochUtc;

    private static Result ValidateEffectiveTimeRange(DateTime startTime, DateTime endTime)
    {
        if (IsDraftTime(startTime) || IsDraftTime(endTime))
        {
            return Result.Success();
        }

        if (startTime >= endTime)
        {
            return Result.Failure(EventErrors.InvalidTimeRange);
        }

        return Result.Success();
    }

    private static bool IsValidTimeZoneId(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return false;
        }

        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }

    private static EventLocation CloneLocation(EventLocation source) =>
        new()
        {
            Name = source.Name,
            StartsAt = source.StartsAt,
            EndsAt = source.EndsAt,
            Kind = source.Kind,
            Url = source.Url,
            Address = source.Address,
            Latitude = source.Latitude,
            Longitude = source.Longitude,
            City = source.City,
            Country = source.Country,
            ExternalPlaceId = source.ExternalPlaceId
        };
}
