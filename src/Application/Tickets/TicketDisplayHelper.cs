using Domain.Events;
using Domain.Tickets;

namespace Application.Tickets;

internal static class TicketDisplayHelper
{
    public static TicketDisplayState GetState(Order order, Event @event, Ticket ticket)
    {
        if (order.Status != OrderStatus.Paid)
        {
            return TicketDisplayState.Unavailable;
        }

        if (@event.Status == EventStatus.Cancelled)
        {
            return TicketDisplayState.RefundPending;
        }

        if (IsCheckedIn(ticket))
        {
            return TicketDisplayState.Used;
        }

        return TicketDisplayState.Active;
    }

    public static bool IsCheckedIn(Ticket ticket) =>
        ticket.Validations.Any(v => v.Status == TicketValidationStatus.Valid);

    public static bool IsRefundPending(Order order, Event @event) =>
        order.Status == OrderStatus.Paid && @event.Status == EventStatus.Cancelled;

    public static DateTime? GetCheckedInAt(Ticket ticket) =>
        ticket.Validations
            .Where(v => v.Status == TicketValidationStatus.Valid)
            .OrderBy(v => v.CreatedAt)
            .Select(v => (DateTime?)v.CreatedAt)
            .FirstOrDefault();
}
