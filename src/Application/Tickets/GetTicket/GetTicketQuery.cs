using Application.Abstractions.Messaging;

namespace Application.Tickets.GetTicket;

public sealed record GetTicketQuery(Guid TicketId) : IQuery<TicketDetailResponse>;
