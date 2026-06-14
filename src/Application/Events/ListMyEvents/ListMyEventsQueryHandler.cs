using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Events.Services;
using Application.Users.Services;
using Domain.Events;
using Domain.Groups;
using Domain.Users.Services;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Events.ListMyEvents;

internal sealed class ListMyEventsQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IUserIdentityAccessor identityAccessor,
    EventAccessService eventAccessService)
    : IQueryHandler<ListMyEventsQuery, IReadOnlyList<MyEventListItemResponse>>
{
    public async Task<Result<IReadOnlyList<MyEventListItemResponse>>> Handle(
        ListMyEventsQuery query,
        CancellationToken cancellationToken)
    {
        var gateResult = VerifiedUserGate.EnsureVerified(identityAccessor);
        if (gateResult.IsFailure)
        {
            return Result.Failure<IReadOnlyList<MyEventListItemResponse>>(gateResult.Error);
        }

        var userResult = await currentUserService.GetOrProvisionAsync(cancellationToken);
        if (userResult.IsFailure)
        {
            return Result.Failure<IReadOnlyList<MyEventListItemResponse>>(userResult.Error);
        }

        var userId = userResult.Value.Id;

        var organizerGroupIds = await context.GroupMemberships
            .AsNoTracking()
            .Where(m => m.UserId == userId &&
                        (m.Role == GroupMemberRole.Organizer ||
                         m.Role == GroupMemberRole.Administrator ||
                         m.Role == GroupMemberRole.Owner))
            .Select(m => m.GroupId)
            .ToListAsync(cancellationToken);

        var events = await context.Events
            .AsNoTracking()
            .Include(e => e.Organizers)
            .Where(e => e.DeletedAt == null &&
                        (e.CreatedByUserId == userId ||
                         e.Organizers.Any(o => o.ParticipantId == userId) ||
                         e.Organizers.Any(o => organizerGroupIds.Contains(o.ParticipantId))))
            .ToListAsync(cancellationToken);

        var hostDisplayNames = await ResolveHostDisplayNamesAsync(events, cancellationToken);

        var items = events
            .Select(e =>
            {
                var hostParticipantId = eventAccessService.GetHostParticipantId(e);
                var hostIsGroup = hostDisplayNames.GroupHostIds.Contains(hostParticipantId);
                return new MyEventListItemResponse(
                    e.Id,
                    e.Tier,
                    e.Title,
                    e.Status,
                    e.StartTime,
                    e.EndTime,
                    hostParticipantId,
                    hostIsGroup,
                    hostDisplayNames.Names.GetValueOrDefault(hostParticipantId, "Host"),
                    e.BannerImageUrl,
                    e.PublishedAt,
                    e.CreatedAt);
            })
            .OrderBy(e => StatusSortKey(e.Status))
            .ThenBy(e => e.StartTime)
            .ThenByDescending(e => e.CreatedAt)
            .ToList();

        return items;
    }

    private static int StatusSortKey(EventStatus status) =>
        status switch
        {
            EventStatus.Draft => 0,
            EventStatus.Published => 1,
            EventStatus.Cancelled => 2,
            _ => 3
        };

    private async Task<HostDisplayNameLookup> ResolveHostDisplayNamesAsync(
        IReadOnlyList<Event> events,
        CancellationToken cancellationToken)
    {
        if (events.Count == 0)
        {
            return new HostDisplayNameLookup([], []);
        }

        var hostParticipantIds = events
            .Select(e => eventAccessService.GetHostParticipantId(e))
            .Distinct()
            .ToList();

        var groups = await context.Groups
            .AsNoTracking()
            .Where(g => hostParticipantIds.Contains(g.Id))
            .Select(g => new { g.Id, g.Name })
            .ToListAsync(cancellationToken);

        var groupHostIds = groups.Select(g => g.Id).ToHashSet();
        var names = groups.ToDictionary(g => g.Id, g => g.Name);

        var userHostIds = hostParticipantIds.Where(id => !groupHostIds.Contains(id)).ToList();

        if (userHostIds.Count > 0)
        {
            var users = await context.Users
                .AsNoTracking()
                .Where(u => userHostIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Username, u.Email })
                .ToListAsync(cancellationToken);

            foreach (var user in users)
            {
                names[user.Id] = !string.IsNullOrWhiteSpace(user.Username)
                    ? user.Username
                    : UserService.CreateDefaultUsernameFromEmail(user.Email);
            }
        }

        return new HostDisplayNameLookup(groupHostIds, names);
    }

    private sealed record HostDisplayNameLookup(
        HashSet<Guid> GroupHostIds,
        Dictionary<Guid, string> Names);
}
