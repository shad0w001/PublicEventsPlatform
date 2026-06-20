using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Events.Services;
using Application.Users.Services;
using Domain.Events;
using Domain.Tickets;
using Domain.Tickets.Services;
using SharedKernel;

namespace Application.Tickets.DeleteEventTicketType;

internal sealed class DeleteEventTicketTypeCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IUserIdentityAccessor identityAccessor,
    EventAccessService eventAccessService)
    : ICommandHandler<DeleteEventTicketTypeCommand>
{
    public async Task<Result> Handle(
        DeleteEventTicketTypeCommand command,
        CancellationToken cancellationToken)
    {
        var gateResult = VerifiedUserGate.EnsureVerified(identityAccessor);
        if (gateResult.IsFailure)
        {
            return gateResult;
        }

        var userResult = await currentUserService.GetOrProvisionAsync(cancellationToken);
        if (userResult.IsFailure)
        {
            return userResult;
        }

        var user = userResult.Value;

        var eventResult = await eventAccessService.GetActiveEventWithTicketTypesAsync(
            command.EventId,
            cancellationToken);

        if (eventResult.IsFailure)
        {
            return eventResult;
        }

        var @event = eventResult.Value;

        if (@event.Status == EventStatus.Cancelled)
        {
            return Result.Failure(EventErrors.CannotModifyCancelled);
        }

        var editAccessResult = await eventAccessService.ResolveEditAccessAsync(
            @event,
            user.Id,
            user.Id,
            cancellationToken);

        if (editAccessResult.IsFailure)
        {
            return editAccessResult;
        }

        var ticketType = @event.TicketTypes.FirstOrDefault(t => t.Id == command.TicketTypeId);
        if (ticketType is null)
        {
            return Result.Failure(TicketErrors.TicketTypeNotFound(command.TicketTypeId));
        }

        var deleteResult = TicketTypeService.Delete(ticketType);
        if (deleteResult.IsFailure)
        {
            return deleteResult;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
