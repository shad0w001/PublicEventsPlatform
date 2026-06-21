using Domain.Events;

namespace Application.Events.Services;

internal sealed record EventDiscoveryCriteria(
    DateTime UtcNow,
    IReadOnlySet<Guid>? ExpandedCategoryIds,
    DateTime? StartFrom,
    DateTime? StartTo,
    IReadOnlyList<EventLocationType>? LocationTypes,
    IReadOnlyList<EventTier>? Tiers,
    IReadOnlyList<AdmissionType>? AdmissionTypes,
    IReadOnlyList<string>? NormalizedCities,
    IReadOnlyList<string>? NormalizedCountries,
    string? Query);
