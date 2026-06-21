using Domain.Events;

namespace Application.Events.BrowseEvents;

public sealed record EventBrowseCardResponse(
    Guid Id,
    string Title,
    string? BannerImageUrl,
    DateTime StartTime,
    DateTime EndTime,
    string? TimeZoneId,
    EventLocationType LocationType,
    AdmissionType? AdmissionType,
    EventTier Tier,
    Guid? CategoryId,
    string? CategoryName,
    string HostDisplayName,
    bool HostIsGroup,
    bool HostIsVerified);
