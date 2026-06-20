using Application.Abstractions.Messaging;

namespace Application.Tickets.GetOrder;

public sealed record GetOrderQuery(Guid OrderId) : IQuery<OrderConfirmationResponse>;
