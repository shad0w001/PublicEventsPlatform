using Domain.Events;
using Domain.Participants;
using SharedKernel;

namespace Domain.Tickets;

public class Ticket : Entity
{
    public Guid OrderId { get; internal set; }
    public Guid TicketTypeId { get; internal set; }
    public Guid EventId { get; internal set; }
    public Guid ParticipantId { get; internal set; }

    public Order Order { get; internal set; } = null!;
    public TicketType TicketType { get; internal set; } = null!;
    public Event Event { get; internal set; } = null!;
    public Participant Participant { get; internal set; } = null!;
    public TicketCode? TicketCode { get; internal set; }
    public List<TicketValidation> Validations { get; internal set; } = [];
}
