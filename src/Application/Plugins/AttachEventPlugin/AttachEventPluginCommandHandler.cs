using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Events.Services;
using Application.Users.Services;
using Domain.Events;
using Domain.Plugins;
using Domain.Plugins.Services;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Plugins.AttachEventPlugin;

internal sealed class AttachEventPluginCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IUserIdentityAccessor identityAccessor,
    EventAccessService eventAccessService)
    : ICommandHandler<AttachEventPluginCommand, EventPluginResponse>
{
    public async Task<Result<EventPluginResponse>> Handle(
        AttachEventPluginCommand command,
        CancellationToken cancellationToken)
    {
        var gateResult = VerifiedUserGate.EnsureVerified(identityAccessor);
        if (gateResult.IsFailure)
        {
            return Result.Failure<EventPluginResponse>(gateResult.Error);
        }

        var userResult = await currentUserService.GetOrProvisionAsync(cancellationToken);
        if (userResult.IsFailure)
        {
            return Result.Failure<EventPluginResponse>(userResult.Error);
        }

        var user = userResult.Value;

        var eventResult = await eventAccessService.GetActiveEventWithPluginsAsync(
            command.EventId,
            includePluginData: false,
            cancellationToken);

        if (eventResult.IsFailure)
        {
            return Result.Failure<EventPluginResponse>(eventResult.Error);
        }

        var @event = eventResult.Value;

        if (@event.Status == EventStatus.Cancelled)
        {
            return Result.Failure<EventPluginResponse>(EventErrors.CannotModifyCancelled);
        }

        var editAccessResult = await eventAccessService.ResolveEditAccessAsync(
            @event,
            user.Id,
            user.Id,
            cancellationToken);

        if (editAccessResult.IsFailure)
        {
            return Result.Failure<EventPluginResponse>(editAccessResult.Error);
        }

        var catalog = await context.Plugins
            .FirstOrDefaultAsync(p => p.Id == command.PluginId, cancellationToken);

        if (catalog is null)
        {
            return Result.Failure<EventPluginResponse>(PluginErrors.NotFound(command.PluginId));
        }

        var attachResult = PluginService.Attach(@event, catalog, command.Data);
        if (attachResult.IsFailure)
        {
            return Result.Failure<EventPluginResponse>(attachResult.Error);
        }

        context.PluginUsages.Add(attachResult.Value);

        await context.SaveChangesAsync(cancellationToken);

        return PluginMapping.ToEventPluginResponse(attachResult.Value, catalog);
    }
}
