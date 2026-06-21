using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.Events.Services;
using Domain.Events;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Events.BrowseEvents;

internal sealed class BrowseEventsQueryHandler(
    IApplicationDbContext context,
    EventAccessService eventAccessService)
    : IQueryHandler<BrowseEventsQuery, PagedResult<EventBrowseCardResponse>>
{
    public async Task<Result<PagedResult<EventBrowseCardResponse>>> Handle(
        BrowseEventsQuery query,
        CancellationToken cancellationToken)
    {
        if (query.StartFrom is not null &&
            query.StartTo is not null &&
            query.StartFrom > query.StartTo)
        {
            return Result.Failure<PagedResult<EventBrowseCardResponse>>(EventDiscoveryErrors.InvalidDateRange);
        }

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(
            query.PageSize,
            1,
            EventDiscoveryConstants.MaxPageSize);

        var utcNow = DateTime.UtcNow;

        IReadOnlySet<Guid>? expandedCategoryIds = null;
        if (query.CategoryId is { Length: > 0 })
        {
            expandedCategoryIds = await EventCategoryExpansionService.ExpandCategoryIdsAsync(
                context,
                query.CategoryId,
                cancellationToken);
        }

        var normalizedCities = query.City is { Length: > 0 }
            ? EventDiscoveryQueryService.NormalizePlaceValues(query.City)
            : null;

        var normalizedCountries = query.Country is { Length: > 0 }
            ? EventDiscoveryQueryService.NormalizePlaceValues(query.Country)
            : null;

        var criteria = new EventDiscoveryCriteria(
            utcNow,
            expandedCategoryIds,
            query.StartFrom,
            query.StartTo,
            query.LocationType,
            query.Tier,
            query.AdmissionType,
            normalizedCities is { Count: > 0 } ? normalizedCities : null,
            normalizedCountries is { Count: > 0 } ? normalizedCountries : null,
            query.Q);

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
