namespace Application.Tickets.CreateEventOrder;

public sealed record CreateEventOrderResponse(
    Guid OrderId,
    string CheckoutUrl,
    DateTime ExpiresAt);
