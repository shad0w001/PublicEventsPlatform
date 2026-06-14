using Domain.Events;

namespace Application.Events;

public sealed record MyEventListItemResponse(
    Guid Id,
    EventTier Tier,
    string Title,
    EventStatus Status,
    DateTime StartTime,
    DateTime EndTime,
    Guid HostParticipantId,
    bool HostIsGroup,
    string HostDisplayName,
    DateTime? PublishedAt,
    DateTime CreatedAt);
