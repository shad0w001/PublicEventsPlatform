using Application.Abstractions.Messaging;

namespace Application.Events.DeleteEvent;

public sealed record DeleteEventCommand(Guid EventId) : ICommand;
