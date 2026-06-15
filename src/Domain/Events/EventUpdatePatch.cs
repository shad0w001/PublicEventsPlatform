using Domain.Events.EventLocations;

namespace Domain.Events;

/// <summary>
/// Partial update payload for EventService.Update. Null properties are left unchanged.
/// When Locations is non-null, the owned collection is replaced entirely.
/// </summary>
public sealed class EventUpdatePatch
{
    public string? Title { get; init; }
    public string? Description { get; init; }
    public string? BannerImageUrl { get; init; }
    public Guid? CategoryId { get; init; }
    public DateTime? StartTime { get; init; }
    public DateTime? EndTime { get; init; }
    public EventTier? Tier { get; init; }
    public string? TimeZoneId { get; init; }
    public AdmissionType? AdmissionType { get; init; }
    public IReadOnlyList<EventLocation>? Locations { get; init; }

    public bool HasAnyField =>
        Title is not null ||
        Description is not null ||
        BannerImageUrl is not null ||
        CategoryId is not null ||
        StartTime is not null ||
        EndTime is not null ||
        Tier is not null ||
        TimeZoneId is not null ||
        AdmissionType is not null ||
        Locations is not null;
}
