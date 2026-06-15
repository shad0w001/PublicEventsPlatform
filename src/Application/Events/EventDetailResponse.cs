using Application.Plugins;
using Domain.Events;

namespace Application.Events;

public sealed record EventDetailResponse(
    Guid Id,
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
    Guid HostParticipantId,
    bool HostIsGroup,
    Guid? CreatedByUserId,
    DateTime CreatedAt,
    DateTime? PublishedAt,
    IReadOnlyList<EventLocationResponse> Locations,
    IReadOnlyList<EventPluginResponse> Plugins);
