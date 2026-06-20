using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Payments;
using Application.Events.Services;
using Application.Payments;
using Application.Users.Services;
using Domain.Tickets;
using Domain.Tickets.Services;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace Application.Tickets.CreateEventOrder;

internal sealed class CreateEventOrderCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IUserIdentityAccessor identityAccessor,
    EventAccessService eventAccessService,
    ITicketTypeRowLock ticketTypeRowLock,
    ICheckoutSessionProvider checkoutSessionProvider,
    IOptions<StripeOptions> stripeOptions) : ICommandHandler<CreateEventOrderCommand, CreateEventOrderResponse>
{
    public async Task<Result<CreateEventOrderResponse>> Handle(
        CreateEventOrderCommand command,
        CancellationToken cancellationToken)
    {
        var gateResult = VerifiedUserGate.EnsureVerified(identityAccessor);
        if (gateResult.IsFailure)
        {
            return Result.Failure<CreateEventOrderResponse>(gateResult.Error);
        }

        var userResult = await currentUserService.GetOrProvisionAsync(cancellationToken);
        if (userResult.IsFailure)
        {
            return Result.Failure<CreateEventOrderResponse>(userResult.Error);
        }

        var user = userResult.Value;

        var participantResult = await eventAccessService.ResolveTicketPurchaseParticipantAsync(
            command.ParticipantId,
            user.Id,
            cancellationToken);

        if (participantResult.IsFailure)
        {
            return Result.Failure<CreateEventOrderResponse>(participantResult.Error);
        }

        var buyerParticipantId = participantResult.Value.ParticipantId;
        var ttlMinutes = stripeOptions.Value.PendingOrderTtlMinutes > 0
            ? stripeOptions.Value.PendingOrderTtlMinutes
            : 30;

        return await ticketTypeRowLock.ExecuteAsync(
            command.TicketTypeId,
            async (ticketType, ct) =>
            {
                if (ticketType.EventId != command.EventId)
                {
                    return Result.Failure<CreateEventOrderResponse>(
                        TicketErrors.TicketTypeEventMismatch);
                }

                var @event = ticketType.Event;
                var expiresAt = DateTime.UtcNow.AddMinutes(ttlMinutes);

                var orderResult = OrderService.CreatePending(
                    @event,
                    ticketType,
                    buyerParticipantId,
                    command.Quantity,
                    expiresAt);

                if (orderResult.IsFailure)
                {
                    return Result.Failure<CreateEventOrderResponse>(orderResult.Error);
                }

                var reserveResult = OrderService.ReserveInventory(ticketType, command.Quantity);
                if (reserveResult.IsFailure)
                {
                    return Result.Failure<CreateEventOrderResponse>(reserveResult.Error);
                }

                var order = orderResult.Value;
                context.Orders.Add(order);

                var successUrl = BuildRedirectUrl(
                    command.SuccessUrl,
                    stripeOptions.Value.SuccessUrlBase,
                    order.Id,
                    isSuccess: true);

                var cancelUrl = BuildRedirectUrl(
                    command.CancelUrl,
                    stripeOptions.Value.CancelUrlBase,
                    command.EventId,
                    isSuccess: false);

                var checkoutResult = await checkoutSessionProvider.CreateAsync(
                    new CheckoutSessionRequest(
                        order.Id,
                        command.EventId,
                        ticketType.Id,
                        ticketType.Name,
                        ticketType.PriceCents,
                        command.Quantity,
                        user.Email,
                        successUrl,
                        cancelUrl,
                        expiresAt),
                    ct);

                if (checkoutResult.IsFailure)
                {
                    return Result.Failure<CreateEventOrderResponse>(checkoutResult.Error);
                }

                var attachResult = OrderService.AttachCheckoutSession(
                    order,
                    checkoutResult.Value.SessionId,
                    checkoutResult.Value.PaymentIntentId);

                if (attachResult.IsFailure)
                {
                    return Result.Failure<CreateEventOrderResponse>(attachResult.Error);
                }

                return new CreateEventOrderResponse(
                    order.Id,
                    checkoutResult.Value.CheckoutUrl,
                    expiresAt);
            },
            cancellationToken);
    }

    private static string BuildRedirectUrl(
        string? requestUrl,
        string configuredBase,
        Guid resourceId,
        bool isSuccess)
    {
        if (!string.IsNullOrWhiteSpace(requestUrl))
        {
            return requestUrl.Trim();
        }

        var baseUrl = configuredBase.TrimEnd('/');
        return isSuccess
            ? $"{baseUrl}/{resourceId}"
            : $"{baseUrl}/{resourceId}";
    }
}
