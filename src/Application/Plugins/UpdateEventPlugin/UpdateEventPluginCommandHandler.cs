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

namespace Application.Plugins.UpdateEventPlugin;

internal sealed class UpdateEventPluginCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IUserIdentityAccessor identityAccessor,
    EventAccessService eventAccessService)
    : ICommandHandler<UpdateEventPluginCommand, EventPluginResponse>
{
    public async Task<Result<EventPluginResponse>> Handle(
        UpdateEventPluginCommand command,
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

        var eventResult = await eventAccessService.GetActiveEventAsync(
            command.EventId,
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

        var usage = await context.PluginUsages
            .Include(u => u.Data)
            .FirstOrDefaultAsync(
                u => u.EventId == command.EventId && u.PluginId == command.PluginId,
                cancellationToken);

        if (usage is null)
        {
            return Result.Failure<EventPluginResponse>(PluginErrors.NotAttached(command.PluginId));
        }

        var updateResult = PluginService.UpdateConfig(
            usage,
            catalog.Code,
            command.Data,
            @event.StartTime,
            @event.EndTime);

        if (updateResult.IsFailure)
        {
            return Result.Failure<EventPluginResponse>(updateResult.Error);
        }

        await context.SaveChangesAsync(cancellationToken);

        return PluginMapping.ToEventPluginResponse(usage, catalog);
    }
}
