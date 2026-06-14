using Application.Abstractions.Messaging;
using Domain.Events;

namespace Application.Events.CreateEvent;

public sealed record CreateEventCommand(
    EventTier Tier,
    string Title,
    Guid? HostId) : ICommand<EventSummaryResponse>;
