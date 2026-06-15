using Application.Abstractions.Messaging;

namespace Application.Events.ListMyEvents;

public sealed record ListMyEventsQuery : IQuery<IReadOnlyList<MyEventListItemResponse>>;
