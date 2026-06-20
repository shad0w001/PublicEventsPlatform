using Application.Abstractions.Messaging;

namespace Application.Tickets.ListMyTickets;

public sealed record ListMyTicketsQuery : IQuery<MyTicketsResponse>;
