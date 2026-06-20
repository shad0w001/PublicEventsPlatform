namespace Domain.Tickets;

public class TicketCode
{
    public Guid TicketId { get; internal set; }
    public string ManualCode { get; internal set; } = string.Empty;

    public Ticket Ticket { get; internal set; } = null!;
}
