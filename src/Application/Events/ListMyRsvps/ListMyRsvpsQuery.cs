using Application.Abstractions.Messaging;

namespace Application.Events.ListMyRsvps;

public sealed record ListMyRsvpsQuery : IQuery<IReadOnlyList<MyRsvpListItemResponse>>;
