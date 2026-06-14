using Application.Abstractions.Messaging;

namespace Application.Events.CancelEvent;

public sealed record CancelEventCommand(Guid EventId) : ICommand;
