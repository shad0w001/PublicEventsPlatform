using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Events.Services;
using Application.Users.Services;
using Domain.Events;
using Domain.Groups;
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
                        (e.Organizers.Any(o => o.ParticipantId == userId) ||
                         e.Organizers.Any(o => organizerGroupIds.Contains(o.ParticipantId))))
            .ToListAsync(cancellationToken);

        var hostParticipantIds = events
            .Select(e => eventAccessService.GetHostParticipantId(e))
            .Distinct()
            .ToList();

        var hostDisplayNames = await EventHostDisplayNameLookup.ResolveBatchAsync(
            context,
            hostParticipantIds,
            cancellationToken);

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
}
