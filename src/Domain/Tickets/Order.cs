using Domain.Events;
using Domain.Participants;
using SharedKernel;

namespace Domain.Tickets;

public class Order : Entity
{
    public Guid EventId { get; internal set; }
    public Guid TicketTypeId { get; internal set; }
    public Guid ParticipantId { get; internal set; }
    public int Quantity { get; internal set; }
    public OrderStatus Status { get; internal set; }
    public string? CheckoutSessionId { get; internal set; }
    public string? PaymentIntentId { get; internal set; }
    public DateTime? ExpiresAt { get; internal set; }

    public Event Event { get; internal set; } = null!;
    public TicketType TicketType { get; internal set; } = null!;
    public Participant Participant { get; internal set; } = null!;
    public List<Ticket> Tickets { get; internal set; } = [];
}
