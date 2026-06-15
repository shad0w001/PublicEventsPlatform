using SharedKernel;

namespace Domain.Events;

public static class EventErrors
{
    public static Error NotFound(Guid eventId) => Error.NotFound(
        "Events.NotFound",
        $"The event with the Id = '{eventId}' was not found");

    public static Error Deleted(Guid eventId) => Error.NotFound(
        "Events.Deleted",
        $"The event with the Id = '{eventId}' has been deleted");

    public static Error InsufficientPermissions() => Error.Forbidden(
        "Events.InsufficientPermissions",
        "You are not authorized to perform this action on this event");

    public static readonly Error InvalidTitle = Error.Validation(
        "Events.InvalidTitle",
        "Event title is required");

    public static Error TitleTooLong(int maxLength) => Error.Validation(
        "Events.TitleTooLong",
        $"Event title must not exceed {maxLength} characters");

    public static readonly Error InvalidDescription = Error.Validation(
        "Events.InvalidDescription",
        "Event description is required");

    public static Error DescriptionTooLong(int maxLength) => Error.Validation(
        "Events.DescriptionTooLong",
        $"Event description must not exceed {maxLength} characters");

    public static readonly Error InvalidTimeRange = Error.Validation(
        "Events.InvalidTimeRange",
        "Event end time must be after start time");

    public static readonly Error DraftTimesNotSet = Error.Validation(
        "Events.DraftTimesNotSet",
        "Event start and end times must be set before publishing");

    public static readonly Error InvalidTimeZoneId = Error.Validation(
        "Events.InvalidTimeZoneId",
        "A valid IANA time zone identifier is required");

    public static readonly Error CategoryRequired = Error.Validation(
        "Events.CategoryRequired",
        "Event category is required to publish");

    public static Error CategoryNotFound(Guid categoryId) => Error.NotFound(
        "Events.CategoryNotFound",
        $"The event category with the Id = '{categoryId}' was not found");

    public static readonly Error AdmissionTypeRequired = Error.Validation(
        "Events.AdmissionTypeRequired",
        "Admission type (free or paid) is required to publish");

    public static readonly Error LocationsRequired = Error.Validation(
        "Events.LocationsRequired",
        "At least one location segment is required to publish");

    public static readonly Error TooManyLocationsForSmallTier = Error.Validation(
        "Events.TooManyLocationsForSmallTier",
        $"Small-tier events may have at most {EventConstants.SmallTierMaxLocations} location segments");

    public static readonly Error InvalidLocationSegment = Error.Validation(
        "Events.InvalidLocationSegment",
        "One or more location segments are invalid");

    public static readonly Error SegmentTimesIncomplete = Error.Validation(
        "Events.SegmentTimesIncomplete",
        "Location segment start and end times must both be set or both omitted");

    public static readonly Error InvalidSegmentTimeRange = Error.Validation(
        "Events.InvalidSegmentTimeRange",
        "Location segment end time must be after start time");

    public static readonly Error SegmentTimeOutOfBounds = Error.Validation(
        "Events.SegmentTimeOutOfBounds",
        "Location segment times must fall within the event start and end times");

    public static readonly Error CannotModifyCancelled = Error.Conflict(
        "Events.CannotModifyCancelled",
        "Cancelled events cannot be modified");

    public static readonly Error CannotModifyDeleted = Error.Conflict(
        "Events.CannotModifyDeleted",
        "Deleted events cannot be modified");

    public static readonly Error CannotDowngradeTier = Error.Conflict(
        "Events.CannotDowngradeTier",
        "Event tier cannot be downgraded after publishing");

    public static readonly Error InvalidStatusTransition = Error.Conflict(
        "Events.InvalidStatusTransition",
        "This status transition is not allowed");

    public static Error PublishRateLimitExceeded(int maxPerWeek) => Error.Conflict(
        "Events.PublishRateLimitExceeded",
        $"Publish limit of {maxPerWeek} events per rolling week has been reached for this host");

    public static readonly Error NoFieldsToUpdate = Error.Validation(
        "Events.NoFieldsToUpdate",
        "At least one field must be provided to update the event");

    public static readonly Error AlreadyPublished = Error.Conflict(
        "Events.AlreadyPublished",
        "The event is already published");

    public static readonly Error NotDraft = Error.Conflict(
        "Events.NotDraft",
        "This action is only allowed on draft events");

    public static readonly Error NotPublished = Error.Conflict(
        "Events.NotPublished",
        "This action is only allowed on published events");

    public static Error HostNotFound(Guid hostId) => Error.NotFound(
        "Events.HostNotFound",
        $"The host participant with the Id = '{hostId}' was not found");

    public static readonly Error InsufficientHostPermissions = Error.Forbidden(
        "Events.InsufficientHostPermissions",
        "You are not authorized to create events for this host");

    public static readonly Error VenueConflict = Error.Conflict(
        "Events.VenueConflict",
        "Another published event already uses this physical venue at an overlapping time");
}
