using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Events.Services;
using Application.Tickets;
using Application.Users.Services;
using Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Tickets.GetOrder;

internal sealed class GetOrderQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IUserIdentityAccessor identityAccessor,
    EventAccessService eventAccessService) : IQueryHandler<GetOrderQuery, OrderConfirmationResponse>
{
    public async Task<Result<OrderConfirmationResponse>> Handle(
        GetOrderQuery query,
        CancellationToken cancellationToken)
    {
        var gateResult = VerifiedUserGate.EnsureVerified(identityAccessor);
        if (gateResult.IsFailure)
        {
            return Result.Failure<OrderConfirmationResponse>(gateResult.Error);
        }

        var userResult = await currentUserService.GetOrProvisionAsync(cancellationToken);
        if (userResult.IsFailure)
        {
            return Result.Failure<OrderConfirmationResponse>(userResult.Error);
        }

        var user = userResult.Value;

        var order = await context.Orders
            .AsNoTracking()
            .Include(o => o.Event)
                .ThenInclude(e => e.Organizers)
            .Include(o => o.TicketType)
            .Include(o => o.Tickets)
            .FirstOrDefaultAsync(o => o.Id == query.OrderId, cancellationToken);

        if (order is null)
        {
            return Result.Failure<OrderConfirmationResponse>(TicketErrors.OrderNotFound(query.OrderId));
        }

        var accessResult = await eventAccessService.CanViewAsBuyerAsync(
            order.ParticipantId,
            user.Id,
            cancellationToken);

        if (accessResult.IsFailure)
        {
            return Result.Failure<OrderConfirmationResponse>(accessResult.Error);
        }

        if (order.Status != OrderStatus.Paid)
        {
            return Result.Failure<OrderConfirmationResponse>(TicketErrors.OrderNotPaid);
        }

        if (order.Event.IsDeleted)
        {
            return Result.Failure<OrderConfirmationResponse>(TicketErrors.OrderNotFound(query.OrderId));
        }

        var hostParticipantId = eventAccessService.GetHostParticipantId(order.Event);
        var hostDisplayNames = await EventHostDisplayNameLookup.ResolveBatchAsync(
            context,
            [hostParticipantId],
            cancellationToken);

        var purchasedAt = order.Tickets.Count > 0
            ? order.Tickets.Min(t => t.CreatedAt)
            : order.CreatedAt;

        var refundPending = TicketDisplayHelper.IsRefundPending(order, order.Event);

        return new OrderConfirmationResponse(
            order.Id,
            order.Status,
            order.Quantity,
            order.TicketType.Name,
            order.TicketType.PriceCents,
            purchasedAt,
            refundPending,
            new OrderConfirmationEventResponse(
                order.Event.Id,
                order.Event.Title,
                order.Event.Status,
                order.Event.StartTime,
                order.Event.EndTime,
                order.Event.TimeZoneId,
                order.Event.BannerImageUrl,
                hostParticipantId,
                hostDisplayNames.GroupHostIds.Contains(hostParticipantId),
                hostDisplayNames.Names.GetValueOrDefault(hostParticipantId, "Host")),
            order.Tickets.Select(t => t.Id).OrderBy(id => id).ToList());
    }
}
