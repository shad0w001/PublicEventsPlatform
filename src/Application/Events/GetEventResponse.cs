namespace Application.Events;

public sealed record GetEventResponse(
    PublicEventResponse? Public,
    EventDetailResponse? EditDetail,
    bool CanEdit);
