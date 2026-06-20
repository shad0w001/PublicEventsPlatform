using Domain.Events;
using SharedKernel;

namespace Domain.Tickets;

public class TicketType : Entity
{
    public Guid EventId { get; internal set; }
    public string Name { get; internal set; } = string.Empty;
    public string Description { get; internal set; } = string.Empty;
    public int PriceCents { get; internal set; }
    public int Capacity { get; internal set; }
    public int SoldQuantity { get; internal set; }
    public int ReservedQuantity { get; internal set; }

    public Event Event { get; internal set; } = null!;
    public List<Ticket> Tickets { get; internal set; } = [];
    public List<Order> Orders { get; internal set; } = [];
}
