using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Events.Services;
using Application.Tickets;
using Application.Users.Services;
using Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Tickets.GetTicket;

internal sealed class GetTicketQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IUserIdentityAccessor identityAccessor,
    EventAccessService eventAccessService) : IQueryHandler<GetTicketQuery, TicketDetailResponse>
{
    public async Task<Result<TicketDetailResponse>> Handle(
        GetTicketQuery query,
        CancellationToken cancellationToken)
    {
        var gateResult = VerifiedUserGate.EnsureVerified(identityAccessor);
        if (gateResult.IsFailure)
        {
            return Result.Failure<TicketDetailResponse>(gateResult.Error);
        }

        var userResult = await currentUserService.GetOrProvisionAsync(cancellationToken);
        if (userResult.IsFailure)
        {
            return Result.Failure<TicketDetailResponse>(userResult.Error);
        }

        var user = userResult.Value;

        var ticket = await context.Tickets
            .AsNoTracking()
            .Include(t => t.TicketType)
            .Include(t => t.TicketCode)
            .Include(t => t.Validations)
            .Include(t => t.Order)
            .Include(t => t.Event)
            .FirstOrDefaultAsync(t => t.Id == query.TicketId, cancellationToken);

        if (ticket is null)
        {
            return Result.Failure<TicketDetailResponse>(TicketErrors.TicketNotFound(query.TicketId));
        }

        var accessResult = await eventAccessService.CanViewAsBuyerAsync(
            ticket.ParticipantId,
            user.Id,
            cancellationToken);

        if (accessResult.IsFailure)
        {
            return Result.Failure<TicketDetailResponse>(accessResult.Error);
        }

        var state = TicketDisplayHelper.GetState(ticket.Order, ticket.Event, ticket);
        var isActive = state == TicketDisplayState.Active;

        return new TicketDetailResponse(
            ticket.Id,
            ticket.OrderId,
            ticket.Event.Title,
            ticket.TicketType.Name,
            state,
            isActive ? ticket.TicketCode?.ManualCode : null,
            isActive ? ticket.Id.ToString() : null);
    }
}
