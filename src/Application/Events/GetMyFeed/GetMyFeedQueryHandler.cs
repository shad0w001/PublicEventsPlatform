using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.Events.BrowseEvents;
using Application.Events.Services;
using Application.Users.Services;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Events.GetMyFeed;

internal sealed class GetMyFeedQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IUserIdentityAccessor identityAccessor,
    EventAccessService eventAccessService)
    : IQueryHandler<GetMyFeedQuery, PagedResult<EventBrowseCardResponse>>
{
    public async Task<Result<PagedResult<EventBrowseCardResponse>>> Handle(
        GetMyFeedQuery query,
        CancellationToken cancellationToken)
    {
        var gateResult = VerifiedUserGate.EnsureVerified(identityAccessor);
        if (gateResult.IsFailure)
        {
            return Result.Failure<PagedResult<EventBrowseCardResponse>>(gateResult.Error);
        }

        var userResult = await currentUserService.GetOrProvisionAsync(cancellationToken);
        if (userResult.IsFailure)
        {
            return Result.Failure<PagedResult<EventBrowseCardResponse>>(userResult.Error);
        }

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(
            query.PageSize,
            1,
            EventDiscoveryConstants.MaxPageSize);

        var subscriptions = await context.UserSubscriptions
            .AsNoTracking()
            .Where(s => s.UserId == userResult.Value.Id)
            .ToListAsync(cancellationToken);

        if (subscriptions.Count == 0)
        {
            return new PagedResult<EventBrowseCardResponse>([], page, pageSize, 0);
        }

        var utcNow = DateTime.UtcNow;
        var criteria = await SubscriptionFeedCriteriaService.ResolveAsync(
            context,
            subscriptions,
            utcNow,
            cancellationToken);

        if (criteria is null)
        {
            return new PagedResult<EventBrowseCardResponse>([], page, pageSize, 0);
        }

        var filteredQuery = EventDiscoveryQueryService.Apply(
            context.Events.AsNoTracking(),
            criteria);

        var totalCount = await filteredQuery.CountAsync(cancellationToken);

        var events = await filteredQuery
            .Include(e => e.Organizers)
            .Include(e => e.Category)
            .OrderBy(e => e.StartTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var hostParticipantIds = events
            .Select(e => eventAccessService.GetHostParticipantId(e))
            .Distinct()
            .ToList();

        var hostDisplayNames = await EventHostDisplayNameLookup.ResolveBatchAsync(
            context,
            hostParticipantIds,
            cancellationToken);

        var items = EventBrowseCardMapper.MapBatch(events, eventAccessService, hostDisplayNames);

        return new PagedResult<EventBrowseCardResponse>(items, page, pageSize, totalCount);
    }
}
