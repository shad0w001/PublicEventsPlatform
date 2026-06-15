using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Events.Services;
using Application.Users.Services;
using Domain.Events.Services;
using SharedKernel;

namespace Application.Events.CancelEvent;

internal sealed class CancelEventCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IUserIdentityAccessor identityAccessor,
    EventAccessService eventAccessService)
    : ICommandHandler<CancelEventCommand>
{
    public async Task<Result> Handle(CancelEventCommand command, CancellationToken cancellationToken)
    {
        var gateResult = VerifiedUserGate.EnsureVerified(identityAccessor);
        if (gateResult.IsFailure)
        {
            return gateResult;
        }

        var userResult = await currentUserService.GetOrProvisionAsync(cancellationToken);
        if (userResult.IsFailure)
        {
            return Result.Failure(userResult.Error);
        }

        var user = userResult.Value;

        var eventResult = await eventAccessService.GetActiveEventAsync(command.EventId, cancellationToken);
        if (eventResult.IsFailure)
        {
            return Result.Failure(eventResult.Error);
        }

        var @event = eventResult.Value;

        var editAccessResult = await eventAccessService.ResolveEditAccessAsync(
            @event,
            user.Id,
            user.Id,
            cancellationToken);

        if (editAccessResult.IsFailure)
        {
            return Result.Failure(editAccessResult.Error);
        }

        var cancelResult = EventService.Cancel(@event);
        if (cancelResult.IsFailure)
        {
            return cancelResult;
        }

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
