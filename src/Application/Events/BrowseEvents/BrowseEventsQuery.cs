using Application.Abstractions.Pagination;
using Application.Abstractions.Messaging;
using Domain.Events;

namespace Application.Events.BrowseEvents;

public sealed record BrowseEventsQuery(
    Guid[]? CategoryId = null,
    DateTime? StartFrom = null,
    DateTime? StartTo = null,
    EventLocationType[]? LocationType = null,
    EventTier[]? Tier = null,
    AdmissionType[]? AdmissionType = null,
    string[]? City = null,
    string[]? Country = null,
    string? Q = null,
    int Page = 1,
    int PageSize = EventDiscoveryConstants.DefaultPageSize) : IQuery<PagedResult<EventBrowseCardResponse>>;
