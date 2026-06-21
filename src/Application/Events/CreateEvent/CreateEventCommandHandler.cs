using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Events.Services;
using Application.Users.Services;
using Domain.Events.Services;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace Application.Events.CreateEvent;

internal sealed class CreateEventCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IUserIdentityAccessor identityAccessor,
    EventAccessService eventAccessService,
    IOptions<EventOptions> eventOptions)
    : ICommandHandler<CreateEventCommand, EventSummaryResponse>
{
    public async Task<Result<EventSummaryResponse>> Handle(
        CreateEventCommand command,
        CancellationToken cancellationToken)
    {
        var gateResult = VerifiedUserGate.EnsureVerified(identityAccessor);
        if (gateResult.IsFailure)
        {
            return Result.Failure<EventSummaryResponse>(gateResult.Error);
        }

        var userResult = await currentUserService.GetOrProvisionAsync(cancellationToken);
        if (userResult.IsFailure)
        {
            return Result.Failure<EventSummaryResponse>(userResult.Error);
        }

        var hostResult = await eventAccessService.ResolveHostForCreateAsync(
            command.HostId,
            userResult.Value.Id,
            cancellationToken);

        if (hostResult.IsFailure)
        {
            return Result.Failure<EventSummaryResponse>(hostResult.Error);
        }

        var host = hostResult.Value;

        var createResult = EventService.Create(
            command.Tier,
            command.Title,
            host.HostParticipantId,
            eventOptions.Value.DefaultBannerUrl);

        if (createResult.IsFailure)
        {
            return Result.Failure<EventSummaryResponse>(createResult.Error);
        }

        var @event = createResult.Value.Event;
        var organizer = createResult.Value.Organizer;

        context.Events.Add(@event);
        context.EventOrganizers.Add(organizer);
        await context.SaveChangesAsync(cancellationToken);

        return new EventSummaryResponse(
            @event.Id,
            @event.Tier,
            @event.Title,
            @event.Status,
            host.HostParticipantId,
            host.HostIsGroup,
            @event.CreatedAt);
    }
}
