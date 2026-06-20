using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Events.Services;
using Application.Users.Services;
using Domain.Events;
using Domain.Users.Services;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Events.ListMyRsvps;

internal sealed class ListMyRsvpsQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IUserIdentityAccessor identityAccessor,
    EventAccessService eventAccessService)
    : IQueryHandler<ListMyRsvpsQuery, IReadOnlyList<MyRsvpListItemResponse>>
{
    public async Task<Result<IReadOnlyList<MyRsvpListItemResponse>>> Handle(
        ListMyRsvpsQuery query,
        CancellationToken cancellationToken)
    {
        var gateResult = VerifiedUserGate.EnsureVerified(identityAccessor);
        if (gateResult.IsFailure)
        {
            return Result.Failure<IReadOnlyList<MyRsvpListItemResponse>>(gateResult.Error);
        }

        var userResult = await currentUserService.GetOrProvisionAsync(cancellationToken);
        if (userResult.IsFailure)
        {
            return Result.Failure<IReadOnlyList<MyRsvpListItemResponse>>(userResult.Error);
        }

        var user = userResult.Value;
        var organizerPlusGroupIds = await eventAccessService.GetOrganizerPlusGroupIdsAsync(
            user.Id,
            cancellationToken);
        var groupIdSet = organizerPlusGroupIds.ToHashSet();
        var participantIds = organizerPlusGroupIds.Append(user.Id).ToList();

        var rows = await context.EventAttendees
            .AsNoTracking()
            .Where(a => participantIds.Contains(a.ParticipantId))
            .Where(a => a.Status == EventAttendeeStatus.Going ||
                        a.Status == EventAttendeeStatus.Interested)
            .Join(
                context.Events.AsNoTracking().Where(e =>
                    e.DeletedAt == null &&
                    e.AdmissionType == AdmissionType.Free &&
                    (e.Status == EventStatus.Published || e.Status == EventStatus.Cancelled)),
                attendee => attendee.EventId,
                @event => @event.Id,
                (attendee, @event) => new { Attendee = attendee, Event = @event })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return Result.Success<IReadOnlyList<MyRsvpListItemResponse>>([]);
        }

        var eventIds = rows.Select(r => r.Event.Id).Distinct().ToList();

        var organizers = await context.EventOrganizers
            .AsNoTracking()
            .Where(o => eventIds.Contains(o.EventId))
            .ToListAsync(cancellationToken);

        var hostParticipantIds = organizers
            .Select(o => o.ParticipantId)
            .Distinct()
            .ToList();

        var hostDisplayNames = await EventHostDisplayNameLookup.ResolveBatchAsync(
            context,
            hostParticipantIds,
            cancellationToken);

        var groupNames = groupIdSet.Count == 0
            ? new Dictionary<Guid, string>()
            : await context.Groups
                .AsNoTracking()
                .Where(g => groupIdSet.Contains(g.Id))
                .ToDictionaryAsync(g => g.Id, g => g.Name, cancellationToken);

        var hostByEventId = organizers.ToDictionary(o => o.EventId, o => o.ParticipantId);

        var selfDisplayName = !string.IsNullOrWhiteSpace(user.Username)
            ? user.Username
            : UserService.CreateDefaultUsernameFromEmail(user.Email);

        var items = rows
            .Select(row =>
            {
                var hostParticipantId = hostByEventId[row.Event.Id];
                var hostIsGroup = hostDisplayNames.GroupHostIds.Contains(hostParticipantId);
                var participantIsGroup = groupIdSet.Contains(row.Attendee.ParticipantId);

                return new MyRsvpListItemResponse(
                    row.Event.Id,
                    row.Event.Tier,
                    row.Event.Title,
                    row.Event.Status,
                    row.Event.StartTime,
                    row.Event.EndTime,
                    row.Event.TimeZoneId,
                    hostParticipantId,
                    hostIsGroup,
                    hostDisplayNames.Names.GetValueOrDefault(hostParticipantId, "Host"),
                    row.Event.BannerImageUrl,
                    row.Attendee.ParticipantId,
                    participantIsGroup,
                    ResolveParticipantDisplayName(
                        row.Attendee.ParticipantId,
                        user.Id,
                        selfDisplayName,
                        groupIdSet,
                        groupNames),
                    row.Attendee.Status!.Value,
                    row.Attendee.RegisteredAt);
            })
            .OrderBy(i => i.StartTime)
            .ThenByDescending(i => i.RegisteredAt)
            .ToList();

        return items;
    }

    private static string ResolveParticipantDisplayName(
        Guid participantId,
        Guid userId,
        string selfDisplayName,
        IReadOnlySet<Guid> groupIds,
        IReadOnlyDictionary<Guid, string> groupNames)
    {
        if (participantId == userId)
        {
            return selfDisplayName;
        }

        if (groupIds.Contains(participantId))
        {
            return groupNames.GetValueOrDefault(participantId, "Organization");
        }

        return "Participant";
    }
}
