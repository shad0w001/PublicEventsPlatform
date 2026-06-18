using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Events.Services;
using Application.Users.Services;
using Domain.Events.Services;
using SharedKernel;

namespace Application.Events.SetEventRsvp;

internal sealed class SetEventRsvpCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IUserIdentityAccessor identityAccessor,
    EventAccessService eventAccessService)
    : ICommandHandler<SetEventRsvpCommand, EventRsvpResponse>
{
    public async Task<Result<EventRsvpResponse>> Handle(
        SetEventRsvpCommand command,
        CancellationToken cancellationToken)
    {
        var gateResult = VerifiedUserGate.EnsureVerified(identityAccessor);
        if (gateResult.IsFailure)
        {
            return Result.Failure<EventRsvpResponse>(gateResult.Error);
        }

        var userResult = await currentUserService.GetOrProvisionAsync(cancellationToken);
        if (userResult.IsFailure)
        {
            return Result.Failure<EventRsvpResponse>(userResult.Error);
        }

        var user = userResult.Value;

        var participantResult = await eventAccessService.ResolveAttendeeParticipantAsync(
            command.ParticipantId,
            user.Id,
            cancellationToken);

        if (participantResult.IsFailure)
        {
            return Result.Failure<EventRsvpResponse>(participantResult.Error);
        }

        var participant = participantResult.Value;

        var eventResult = await eventAccessService.GetActiveEventWithAttendeesAsync(
            command.EventId,
            cancellationToken);

        if (eventResult.IsFailure)
        {
            return Result.Failure<EventRsvpResponse>(eventResult.Error);
        }

        var @event = eventResult.Value;
        var hostParticipantId = eventAccessService.GetHostParticipantId(@event);
        var utcNow = DateTime.UtcNow;

        var rsvpResult = EventAttendeeService.SetRsvpStatus(
            @event,
            participant.ParticipantId,
            hostParticipantId,
            command.Status,
            utcNow);

        if (rsvpResult.IsFailure)
        {
            return Result.Failure<EventRsvpResponse>(rsvpResult.Error);
        }

        var attendee = rsvpResult.Value;
        await context.SaveChangesAsync(cancellationToken);

        return new EventRsvpResponse(
            @event.Id,
            attendee.ParticipantId,
            participant.ParticipantIsGroup,
            attendee.Status,
            attendee.RegisteredAt);
    }
}
