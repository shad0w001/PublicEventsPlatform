using Application.Abstractions.Messaging;

namespace Application.Events.PublishEvent;

public sealed record PublishEventCommand(Guid EventId) : ICommand<EventDetailResponse>;