using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.Events;
using Application.Events.BrowseEvents;

namespace Application.Events.GetMyFeed;

public sealed record GetMyFeedQuery(
    int Page = 1,
    int PageSize = EventDiscoveryConstants.DefaultPageSize) : IQuery<PagedResult<EventBrowseCardResponse>>;
