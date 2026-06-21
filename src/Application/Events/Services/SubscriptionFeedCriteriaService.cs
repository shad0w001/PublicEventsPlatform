using Application.Abstractions.Data;
using Domain.Subscriptions;

namespace Application.Events.Services;

internal static class SubscriptionFeedCriteriaService
{
    public static async Task<EventDiscoveryCriteria?> ResolveAsync(
        IApplicationDbContext context,
        IReadOnlyList<UserSubscription> subscriptions,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        if (subscriptions.Count == 0)
        {
            return null;
        }

        var normalizedCities = subscriptions
            .Where(s => s.Kind == SubscriptionKind.City && s.City is not null)
            .Select(s => s.City!)
            .Distinct()
            .ToList();

        var includeVirtualSegment = subscriptions.Any(s => s.Kind == SubscriptionKind.Online);

        var categoryIds = subscriptions
            .Where(s => s.Kind == SubscriptionKind.Category && s.CategoryId is not null)
            .Select(s => s.CategoryId!.Value)
            .Distinct()
            .ToList();

        IReadOnlySet<Guid>? expandedCategoryIds = null;
        if (categoryIds.Count > 0)
        {
            expandedCategoryIds = await EventCategoryExpansionService.ExpandCategoryIdsAsync(
                context,
                categoryIds,
                cancellationToken);
        }

        var hasLocationFilter = normalizedCities.Count > 0 || includeVirtualSegment;
        var hasCategoryFilter = expandedCategoryIds is { Count: > 0 };

        if (!hasLocationFilter && !hasCategoryFilter)
        {
            return null;
        }

        return new EventDiscoveryCriteria(
            utcNow,
            hasCategoryFilter ? expandedCategoryIds : null,
            StartFrom: null,
            StartTo: null,
            LocationTypes: null,
            Tiers: null,
            AdmissionTypes: null,
            hasLocationFilter && normalizedCities.Count > 0 ? normalizedCities : null,
            NormalizedCountries: null,
            Query: null,
            IncludeVirtualSegmentForLocationMatch: hasLocationFilter && includeVirtualSegment);
    }
}
