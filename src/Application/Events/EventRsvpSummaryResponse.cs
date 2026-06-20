using Domain.Events;

namespace Application.Events;

public sealed record EventRsvpSummaryResponse(
    int GoingCount,
    int InterestedCount,
    int ResponseCount,
    IReadOnlyList<EventRsvpMyStatusResponse>? MyStatuses);

public sealed record EventRsvpMyStatusResponse(
    Guid ParticipantId,
    bool ParticipantIsGroup,
    EventAttendeeStatus Status);
