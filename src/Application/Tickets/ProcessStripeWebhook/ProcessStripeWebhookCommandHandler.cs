using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Payments;
using Domain.Events.Services;
using Domain.Tickets;
using Domain.Tickets.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Application.Tickets.ProcessStripeWebhook;

internal sealed class ProcessStripeWebhookCommandHandler(
    IApplicationDbContext context,
    IStripeWebhookVerifier webhookVerifier,
    ITicketTypeRowLock ticketTypeRowLock,
    ILogger<ProcessStripeWebhookCommandHandler> logger) : ICommandHandler<ProcessStripeWebhookCommand>
{
    public async Task<Result> Handle(
        ProcessStripeWebhookCommand command,
        CancellationToken cancellationToken)
    {
        var verifyResult = await webhookVerifier.VerifyAsync(
            command.Json,
            command.StripeSignatureHeader,
            cancellationToken);

        if (verifyResult.IsFailure)
        {
            return Result.Failure(verifyResult.Error);
        }

        var webhookEvent = verifyResult.Value;
        if (webhookEvent.EventType == StripeWebhookEventType.Ignored)
        {
            return Result.Success();
        }

        var order = await context.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == webhookEvent.OrderId, cancellationToken);

        if (order is null)
        {
            return Result.Failure(TicketErrors.OrderNotFound(webhookEvent.OrderId));
        }

        if (!string.Equals(order.CheckoutSessionId, webhookEvent.SessionId, StringComparison.Ordinal))
        {
            return Result.Failure(TicketErrors.CheckoutSessionMismatch);
        }

        if (order.EventId != webhookEvent.EventId ||
            order.TicketTypeId != webhookEvent.TicketTypeId)
        {
            return Result.Failure(TicketErrors.TicketTypeEventMismatch);
        }

        var lockResult = await ticketTypeRowLock.ExecuteAsync(
            webhookEvent.TicketTypeId,
            async (ticketType, ct) =>
            {
                var processResult = await ProcessWithinLockAsync(webhookEvent, ticketType, ct);
                return processResult.IsSuccess
                    ? Result.Success(true)
                    : Result.Failure<bool>(processResult.Error);
            },
            cancellationToken);

        return lockResult.IsFailure
            ? Result.Failure(lockResult.Error)
            : Result.Success();
    }

    private async Task<Result> ProcessWithinLockAsync(
        StripeWebhookEvent webhookEvent,
        TicketType ticketType,
        CancellationToken cancellationToken)
    {
        var order = await context.Orders
            .Include(o => o.Tickets)
            .FirstOrDefaultAsync(o => o.Id == webhookEvent.OrderId, cancellationToken);

        if (order is null)
        {
            return Result.Failure(TicketErrors.OrderNotFound(webhookEvent.OrderId));
        }

        if (!string.Equals(order.CheckoutSessionId, webhookEvent.SessionId, StringComparison.Ordinal))
        {
            return Result.Failure(TicketErrors.CheckoutSessionMismatch);
        }

        return webhookEvent.EventType switch
        {
            StripeWebhookEventType.CheckoutSessionCompleted => await HandleCompletedAsync(
                webhookEvent,
                order,
                ticketType,
                cancellationToken),
            StripeWebhookEventType.CheckoutSessionExpired => HandleExpired(
                order,
                ticketType),
            _ => Result.Success()
        };
    }

    private async Task<Result> HandleCompletedAsync(
        StripeWebhookEvent webhookEvent,
        Order order,
        TicketType ticketType,
        CancellationToken cancellationToken)
    {
        if (order.Status is OrderStatus.Expired or OrderStatus.Cancelled)
        {
            logger.LogWarning(
                "Received checkout.session.completed for {OrderStatus} order {OrderId}; acknowledging for manual refund handling",
                order.Status,
                order.Id);
            return Result.Success();
        }

        if (order.Status == OrderStatus.Paid)
        {
            if (order.Tickets.Count >= order.Quantity)
            {
                return Result.Success();
            }

            return await CompleteFulfillmentAsync(order, ticketType, webhookEvent, cancellationToken);
        }

        if (order.Status != OrderStatus.Pending)
        {
            return Result.Success();
        }

        var markPaidResult = OrderService.MarkPaid(order, ticketType, order.Quantity);
        if (markPaidResult.IsFailure)
        {
            return markPaidResult;
        }

        return await CompleteFulfillmentAsync(order, ticketType, webhookEvent, cancellationToken);
    }

    private async Task<Result> CompleteFulfillmentAsync(
        Order order,
        TicketType ticketType,
        StripeWebhookEvent webhookEvent,
        CancellationToken cancellationToken)
    {
        var issueResult = TicketService.IssueTickets(
            order,
            ticketType,
            order.ParticipantId,
            order.Quantity,
            code => context.TicketCodes.Any(ticketCode => ticketCode.ManualCode == code));

        if (issueResult.IsFailure)
        {
            return issueResult;
        }

        foreach (var ticket in issueResult.Value)
        {
            context.Tickets.Add(ticket);
        }

        var @event = await context.Events
            .Include(e => e.Attendees)
            .FirstAsync(e => e.Id == order.EventId, cancellationToken);

        var attendanceResult = EventAttendeeService.UpsertPaidAttendance(
            @event,
            order.ParticipantId,
            order.Quantity);

        if (attendanceResult.IsFailure)
        {
            return attendanceResult;
        }

        var paymentIntentResult = OrderService.EnsurePaymentIntentId(
            order,
            webhookEvent.PaymentIntentId);

        return paymentIntentResult;
    }

    private static Result HandleExpired(Order order, TicketType ticketType)
    {
        if (order.Status == OrderStatus.Expired || order.Status == OrderStatus.Paid)
        {
            return Result.Success();
        }

        if (order.Status != OrderStatus.Pending)
        {
            return Result.Success();
        }

        return OrderService.MarkExpired(order, ticketType, order.Quantity);
    }
}
