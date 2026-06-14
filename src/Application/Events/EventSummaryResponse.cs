using Domain.Events;

namespace Application.Events;

public sealed record EventSummaryResponse(
    Guid Id,
    EventTier Tier,
    string Title,
    EventStatus Status,
    Guid HostParticipantId,
    bool HostIsGroup,
    DateTime CreatedAt);
