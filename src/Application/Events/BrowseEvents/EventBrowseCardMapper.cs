using Application.Events.Services;
using Domain.Events;

namespace Application.Events.BrowseEvents;

internal static class EventBrowseCardMapper
{
    public static IReadOnlyList<EventBrowseCardResponse> MapBatch(
        IReadOnlyList<Event> events,
        EventAccessService eventAccessService,
        EventHostDisplayNameLookup.HostDisplayNameLookup hostDisplayNames) =>
        events
            .Select(e =>
            {
                var hostParticipantId = eventAccessService.GetHostParticipantId(e);
                var hostIsGroup = hostDisplayNames.GroupHostIds.Contains(hostParticipantId);
                var hostIsVerified = hostIsGroup &&
                                     hostDisplayNames.VerifiedGroupHostIds.Contains(hostParticipantId);
                return new EventBrowseCardResponse(
                    e.Id,
                    e.Title,
                    e.BannerImageUrl,
                    e.StartTime,
                    e.EndTime,
                    e.TimeZoneId,
                    e.LocationType,
                    e.AdmissionType,
                    e.Tier,
                    e.CategoryId,
                    e.Category?.Name,
                    hostDisplayNames.Names.GetValueOrDefault(hostParticipantId, "Host"),
                    hostIsGroup,
                    hostIsVerified);
            })
            .ToList();
}
