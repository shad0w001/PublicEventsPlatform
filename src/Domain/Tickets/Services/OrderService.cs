using Domain.Events;
using Domain.Tickets.Events;
using SharedKernel;

namespace Domain.Tickets.Services;

public static class OrderService
{
    public static Result<Order> CreatePending(
        Event @event,
        TicketType ticketType,
        Guid participantId,
        int quantity,
        DateTime expiresAt)
    {
        if (@event.Status == EventStatus.Cancelled)
        {
            return Result.Failure<Order>(TicketErrors.EventCancelled);
        }

        if (@event.Status != EventStatus.Published)
        {
            return Result.Failure<Order>(TicketErrors.EventNotPublished);
        }

        if (@event.AdmissionType != AdmissionType.Paid)
        {
            return Result.Failure<Order>(TicketErrors.PaidAdmissionRequired);
        }

        if (ticketType.EventId != @event.Id)
        {
            return Result.Failure<Order>(TicketErrors.TicketTypeEventMismatch);
        }

        var remaining = TicketTypeService.GetRemainingQuantity(ticketType);
        if (quantity < 1 || quantity > remaining)
        {
            return Result.Failure<Order>(TicketErrors.InvalidQuantity);
        }

        var order = new Order
        {
            EventId = @event.Id,
            TicketTypeId = ticketType.Id,
            ParticipantId = participantId,
            Quantity = quantity,
            Status = OrderStatus.Pending,
            ExpiresAt = expiresAt,
            Event = @event,
            TicketType = ticketType
        };

        return order;
    }

    public static Result AttachCheckoutSession(
        Order order,
        string checkoutSessionId,
        string? paymentIntentId)
    {
        if (order.Status != OrderStatus.Pending)
        {
            return Result.Failure(TicketErrors.OrderNotPending);
        }

        if (string.IsNullOrWhiteSpace(checkoutSessionId))
        {
            return Result.Failure(TicketErrors.InvalidCheckoutSession);
        }

        order.CheckoutSessionId = checkoutSessionId.Trim();
        order.PaymentIntentId = string.IsNullOrWhiteSpace(paymentIntentId)
            ? null
            : paymentIntentId.Trim();

        return Result.Success();
    }

    public static Result ReserveInventory(TicketType ticketType, int quantity)
    {
        var remaining = TicketTypeService.GetRemainingQuantity(ticketType);
        if (quantity < 1 || quantity > remaining)
        {
            return Result.Failure(TicketErrors.InsufficientInventory);
        }

        ticketType.ReservedQuantity += quantity;
        return Result.Success();
    }

    public static Result ReleaseReservation(TicketType ticketType, int quantity)
    {
        if (quantity < 1 || quantity > ticketType.ReservedQuantity)
        {
            return Result.Failure(TicketErrors.OrderQuantityMismatch);
        }

        ticketType.ReservedQuantity -= quantity;
        return Result.Success();
    }

    public static Result MarkPaid(Order order, TicketType ticketType, int quantity)
    {
        if (order.Status != OrderStatus.Pending)
        {
            return Result.Failure(TicketErrors.OrderNotPending);
        }

        if (order.Quantity != quantity || order.TicketTypeId != ticketType.Id)
        {
            return Result.Failure(TicketErrors.OrderQuantityMismatch);
        }

        if (ticketType.ReservedQuantity < quantity)
        {
            return Result.Failure(TicketErrors.InsufficientInventory);
        }

        ticketType.ReservedQuantity -= quantity;
        ticketType.SoldQuantity += quantity;
        order.Status = OrderStatus.Paid;

        order.Raise(new TicketPurchaseCompleted(
            order.Id,
            order.EventId,
            order.ParticipantId,
            quantity));

        return Result.Success();
    }

    public static Result MarkExpired(Order order, TicketType ticketType, int quantity)
    {
        if (order.Status != OrderStatus.Pending)
        {
            return Result.Failure(TicketErrors.OrderNotPending);
        }

        if (order.Quantity != quantity || order.TicketTypeId != ticketType.Id)
        {
            return Result.Failure(TicketErrors.OrderQuantityMismatch);
        }

        var releaseResult = ReleaseReservation(ticketType, quantity);
        if (releaseResult.IsFailure)
        {
            return releaseResult;
        }

        order.Status = OrderStatus.Expired;
        return Result.Success();
    }

    public static Result MarkCancelled(Order order, TicketType ticketType, int quantity)
    {
        if (order.Status != OrderStatus.Pending)
        {
            return Result.Failure(TicketErrors.OrderNotPending);
        }

        if (order.Quantity != quantity || order.TicketTypeId != ticketType.Id)
        {
            return Result.Failure(TicketErrors.OrderQuantityMismatch);
        }

        var releaseResult = ReleaseReservation(ticketType, quantity);
        if (releaseResult.IsFailure)
        {
            return releaseResult;
        }

        order.Status = OrderStatus.Cancelled;
        return Result.Success();
    }
}
