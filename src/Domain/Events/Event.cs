using Domain.Events.EventLocations;
using Domain.Plugins;
using SharedKernel;

namespace Domain.Events;

public class Event : Entity
{
    public string Title { get; internal set; } = string.Empty;
    public Guid? CategoryId { get; internal set; }
    public string Description { get; internal set; } = string.Empty;
    public string? BannerImageUrl { get; internal set; }
    public DateTime StartTime { get; internal set; } = EventConstants.DraftEpochUtc;
    public DateTime EndTime { get; internal set; } = EventConstants.DraftEpochUtc;
    public EventStatus Status { get; internal set; } = EventStatus.Draft;
    public EventLocationType LocationType { get; internal set; } = EventLocationType.Physical;
    public EventTier Tier { get; internal set; }
    public string? TimeZoneId { get; internal set; }
    public AdmissionType? AdmissionType { get; internal set; }
    public DateTime? PublishedAt { get; internal set; }
    public DateTime? DeletedAt { get; internal set; }
    public Guid? CreatedByUserId { get; internal set; }

    public bool IsDeleted => DeletedAt is not null;

    public EventCategory? Category { get; internal set; }
    public List<EventLocation> Locations { get; internal set; } = [];
    public List<EventOrganizer> Organizers { get; internal set; } = [];
    public List<EventAttendee> Attendees { get; internal set; } = [];
    public List<PluginUsage> Plugins { get; internal set; } = [];
}
