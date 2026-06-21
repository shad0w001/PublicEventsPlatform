using Domain.Events;
using Domain.Events.EventLocations;
using Microsoft.EntityFrameworkCore;

namespace Application.Events.Services;

internal static class EventDiscoveryQueryService
{
    public static IReadOnlyList<string> NormalizePlaceValues(IEnumerable<string> values) =>
        values
            .Select(EventVenueRules.NormalizePlaceComponent)
            .Where(v => v is not null)
            .Cast<string>()
            .Distinct()
            .ToList();

    public static IQueryable<Event> Apply(IQueryable<Event> query, EventDiscoveryCriteria criteria)
    {
        query = query.Where(e =>
            e.DeletedAt == null &&
            e.Status == EventStatus.Published &&
            e.StartTime >= criteria.UtcNow);

        if (criteria.ExpandedCategoryIds is { Count: > 0 } categoryIds)
        {
            query = query.Where(e =>
                e.CategoryId != null && categoryIds.Contains(e.CategoryId.Value));
        }

        if (criteria.StartFrom is not null)
        {
            query = query.Where(e => e.StartTime >= criteria.StartFrom.Value);
        }

        if (criteria.StartTo is not null)
        {
            query = query.Where(e => e.StartTime <= criteria.StartTo.Value);
        }

        if (criteria.LocationTypes is { Count: > 0 } locationTypes)
        {
            query = query.Where(e => locationTypes.Contains(e.LocationType));
        }

        if (criteria.Tiers is { Count: > 0 } tiers)
        {
            query = query.Where(e => tiers.Contains(e.Tier));
        }

        if (criteria.AdmissionTypes is { Count: > 0 } admissionTypes)
        {
            query = query.Where(e =>
                e.AdmissionType != null && admissionTypes.Contains(e.AdmissionType.Value));
        }

        var hasCityFilter = criteria.NormalizedCities is { Count: > 0 };
        var hasOnlineFeedFilter = criteria.IncludeVirtualSegmentForLocationMatch;

        if (hasCityFilter || hasOnlineFeedFilter)
        {
            query = query.Where(e =>
                (hasCityFilter &&
                 e.Locations.Any(l =>
                     l.Kind == EventLocationKind.Physical &&
                     l.City != null &&
                     criteria.NormalizedCities!.Contains(l.City.ToLower().Trim()))) ||
                (hasOnlineFeedFilter &&
                 e.Locations.Any(l => l.Kind == EventLocationKind.Virtual)));
        }

        if (criteria.NormalizedCountries is { Count: > 0 } countries)
        {
            query = query.Where(e => e.Locations.Any(l =>
                l.Kind == EventLocationKind.Physical &&
                l.Country != null &&
                countries.Contains(l.Country.ToLower().Trim())));
        }

        if (!string.IsNullOrWhiteSpace(criteria.Query))
        {
            var term = criteria.Query.Trim().ToLower();
            query = query.Where(e =>
                e.Title.ToLower().Contains(term) ||
                e.Description.ToLower().Contains(term) ||
                e.LocationType.ToString().ToLower().Contains(term) ||
                e.Locations.Any(l =>
                    l.Kind == EventLocationKind.Physical &&
                    l.City != null &&
                    l.City.ToLower().Contains(term)) ||
                (e.Category != null && e.Category.Name.ToLower().Contains(term)));
        }

        return query;
    }
}
