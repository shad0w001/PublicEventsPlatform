using Domain.Events;

namespace Application.Events;

public sealed record MyRsvpListItemResponse(
    Guid EventId,
    EventTier Tier,
    string Title,
    EventStatus Status,
    DateTime StartTime,
    DateTime EndTime,
    string TimeZoneId,
    Guid HostParticipantId,
    bool HostIsGroup,
    string HostDisplayName,
    string? BannerImageUrl,
    Guid ParticipantId,
    bool ParticipantIsGroup,
    string ParticipantDisplayName,
    EventAttendeeStatus RsvpStatus,
    DateTime? RegisteredAt);
