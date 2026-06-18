using Application.Plugins;
using Domain.Events;

namespace Application.Events;

public sealed record PublicEventResponse(
    EventTier Tier,
    string Title,
    string Description,
    string? BannerImageUrl,
    Guid? CategoryId,
    string? CategoryName,
    DateTime StartTime,
    DateTime EndTime,
    string? TimeZoneId,
    AdmissionType? AdmissionType,
    EventStatus Status,
    EventLocationType LocationType,
    DateTime? PublishedAt,
    string HostDisplayName,
    bool HostIsGroup,
    IReadOnlyList<EventLocationResponse> Locations,
    IReadOnlyList<EventPluginResponse> Plugins,
    EventRsvpSummaryResponse? RsvpSummary = null);
