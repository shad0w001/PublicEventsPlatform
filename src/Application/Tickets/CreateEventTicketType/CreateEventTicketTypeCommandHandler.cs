using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Events.Services;
using Application.Users.Services;
using Domain.Events;
using Domain.Tickets.Services;
using SharedKernel;

namespace Application.Tickets.CreateEventTicketType;

internal sealed class CreateEventTicketTypeCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IUserIdentityAccessor identityAccessor,
    EventAccessService eventAccessService)
    : ICommandHandler<CreateEventTicketTypeCommand, TicketTypeResponse>
{
    public async Task<Result<TicketTypeResponse>> Handle(
        CreateEventTicketTypeCommand command,
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

        var createResult = TicketTypeService.Create(
            @event,
            command.Name,
            command.Description,
            command.PriceCents,
            command.Capacity);

        if (createResult.IsFailure)
        {
            return Result.Failure<TicketTypeResponse>(createResult.Error);
        }

        context.TicketTypes.Add(createResult.Value);

        await context.SaveChangesAsync(cancellationToken);

        return TicketMapping.ToResponse(createResult.Value);
    }
}
