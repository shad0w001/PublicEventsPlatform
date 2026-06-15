using Application.Abstractions.Messaging;

namespace Application.Events.GetEvent;

public sealed record GetEventQuery(Guid EventId) : IQuery<GetEventResponse>;
