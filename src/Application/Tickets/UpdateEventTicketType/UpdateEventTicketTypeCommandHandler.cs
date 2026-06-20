using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Events.Services;
using Application.Users.Services;
using Domain.Events;
using Domain.Tickets;
using Domain.Tickets.Services;
using SharedKernel;

namespace Application.Tickets.UpdateEventTicketType;

internal sealed class UpdateEventTicketTypeCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IUserIdentityAccessor identityAccessor,
    EventAccessService eventAccessService)
    : ICommandHandler<UpdateEventTicketTypeCommand, TicketTypeResponse>
{
    public async Task<Result<TicketTypeResponse>> Handle(
        UpdateEventTicketTypeCommand command,
        CancellationToken cancellationToken)
    {
        var gateResult = VerifiedUserGate.EnsureVerified(identityAccessor);
        if (gateResult.IsFailure)
        {
            return Result.Failure<TicketTypeResponse>(gateResult.Error);
        }

        var userResult = await currentUserService.GetOrProvisionAsync(cancellationToken);
        if (userResult.IsFailure)
        {
            return Result.Failure<TicketTypeResponse>(userResult.Error);
        }

        var user = userResult.Value;

        var eventResult = await eventAccessService.GetActiveEventWithTicketTypesAsync(
            command.EventId,
            cancellationToken);

        if (eventResult.IsFailure)
        {
            return Result.Failure<TicketTypeResponse>(eventResult.Error);
        }

        var @event = eventResult.Value;

        if (@event.Status == EventStatus.Cancelled)
        {
            return Result.Failure<TicketTypeResponse>(EventErrors.CannotModifyCancelled);
        }

        var editAccessResult = await eventAccessService.ResolveEditAccessAsync(
            @event,
            user.Id,
            user.Id,
            cancellationToken);

        if (editAccessResult.IsFailure)
        {
            return Result.Failure<TicketTypeResponse>(editAccessResult.Error);
        }

        var ticketType = @event.TicketTypes.FirstOrDefault(t => t.Id == command.TicketTypeId);
        if (ticketType is null)
        {
            return Result.Failure<TicketTypeResponse>(TicketErrors.TicketTypeNotFound(command.TicketTypeId));
        }

        var updateResult = TicketTypeService.Update(
            ticketType,
            command.Name,
            command.Description,
            command.PriceCents,
            command.Capacity);

        if (updateResult.IsFailure)
        {
            return Result.Failure<TicketTypeResponse>(updateResult.Error);
        }

        await context.SaveChangesAsync(cancellationToken);

        return TicketMapping.ToResponse(ticketType);
    }
}
