using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Events.Services;
using Application.Users.Services;
using Domain.Events;
using Domain.Plugins.Services;
using SharedKernel;

namespace Application.Plugins.DetachEventPlugin;

internal sealed class DetachEventPluginCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IUserIdentityAccessor identityAccessor,
    EventAccessService eventAccessService)
    : ICommandHandler<DetachEventPluginCommand>
{
    public async Task<Result> Handle(
        DetachEventPluginCommand command,
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

        var eventResult = await eventAccessService.GetActiveEventWithPluginsAsync(
            command.EventId,
            includePluginData: false,
            cancellationToken);

        if (eventResult.IsFailure)
        {
            return Result.Failure(eventResult.Error);
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
            return Result.Failure(editAccessResult.Error);
        }

        var detachResult = PluginService.Detach(@event, command.PluginId);
        if (detachResult.IsFailure)
        {
            return detachResult;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
