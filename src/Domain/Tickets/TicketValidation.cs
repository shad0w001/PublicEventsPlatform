using SharedKernel;

namespace Domain.Tickets;

public class TicketValidation : Entity
{
    public Guid TicketId { get; internal set; }
    public Guid ValidatedByUserId { get; internal set; }
    public TicketValidationMethod Method { get; internal set; }
    public TicketValidationStatus Status { get; internal set; }

    public Ticket Ticket { get; internal set; } = null!;
}
