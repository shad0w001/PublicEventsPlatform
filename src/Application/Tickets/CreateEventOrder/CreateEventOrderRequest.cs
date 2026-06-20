namespace Application.Tickets.CreateEventOrder;

public sealed record CreateEventOrderRequest(
    Guid TicketTypeId,
    int Quantity,
    Guid? ParticipantId = null,
    string? SuccessUrl = null,
    string? CancelUrl = null);
