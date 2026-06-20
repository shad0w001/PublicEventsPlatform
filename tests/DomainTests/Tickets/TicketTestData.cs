using Domain.Events;
using Domain.Events.Services;
using Domain.Tickets;
using Domain.Tickets.Services;
using DomainTests.Events;

namespace DomainTests.Tickets;

internal static class TicketTestData
{
    internal static readonly Guid BuyerParticipantId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
    internal static readonly Guid ValidatorUserId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
    internal static readonly DateTime DefaultExpiresAt = new(2026, 6, 20, 13, 0, 0, DateTimeKind.Utc);

    internal static Event MakePaidPublished()
    {
        var draft = MakePaidDraft();
        CreateTicketType(draft);

        var publishResult = EventService.Publish(
            draft,
            categoryExists: true,
            recentPublishCount: 0,
            maxPublishesPerWeek: 6);

        if (publishResult.IsFailure)
        {
            throw new InvalidOperationException(publishResult.Error.Message);
        }

        return draft;
    }

    internal static Event MakePaidDraft()
    {
        var (draft, _) = EventTestData.CreateDraft();
        EventTestData.MakePublishReady(draft);
        draft.AdmissionType = AdmissionType.Paid;
        return draft;
    }

    internal static TicketType CreateTicketType(
        Event @event,
        string name = "General Admission",
        int priceCents = 2500,
        int capacity = 100) =>
        TicketTypeService.Create(@event, name, "Standard entry", priceCents, capacity).Value;

    internal static (Order Order, TicketType TicketType) CreatePendingOrder(
        Event @event,
        TicketType? ticketType = null,
        int quantity = 2)
    {
        var type = ticketType ?? CreateTicketType(@event);
        var orderResult = OrderService.CreatePending(
            @event,
            type,
            BuyerParticipantId,
            quantity,
            DefaultExpiresAt);

        if (orderResult.IsFailure)
        {
            throw new InvalidOperationException(orderResult.Error.Message);
        }

        var reserveResult = OrderService.ReserveInventory(type, quantity);
        if (reserveResult.IsFailure)
        {
            throw new InvalidOperationException(reserveResult.Error.Message);
        }

        return (orderResult.Value, type);
    }

    internal static (Order Order, TicketType TicketType, IReadOnlyList<Ticket> Tickets) CreatePaidOrderWithTickets(
        Event @event,
        TicketType? ticketType = null,
        int quantity = 2)
    {
        var (order, type) = CreatePendingOrder(@event, ticketType, quantity);

        var payResult = OrderService.MarkPaid(order, type, quantity);
        if (payResult.IsFailure)
        {
            throw new InvalidOperationException(payResult.Error.Message);
        }

        var ticketsResult = TicketService.IssueTickets(order, type, BuyerParticipantId, quantity);
        if (ticketsResult.IsFailure)
        {
            throw new InvalidOperationException(ticketsResult.Error.Message);
        }

        return (order, type, ticketsResult.Value);
    }

    internal static Ticket CreateTicketWithCode(
        Event @event,
        string manualCode = "AB12CD34")
    {
        var (_, _, tickets) = CreatePaidOrderWithTickets(@event, quantity: 1);
        var ticket = tickets[0];
        ticket.TicketCode!.ManualCode = manualCode;
        return ticket;
    }
}
