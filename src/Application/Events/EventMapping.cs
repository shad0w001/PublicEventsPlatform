using Application.Plugins;
using Application.Events.Services;
using Domain.Events;
using Domain.Events.EventLocations;

namespace Application.Events;

internal static class EventMapping
{
    public static EventDetailResponse ToDetailResponse(
        Event @event,
        EventEditAccess editAccess,
        string? categoryName,
        IReadOnlyList<EventPluginResponse>? plugins = null,
        EventRsvpSummaryResponse? rsvpSummary = null) =>
        new(
            @event.Id,
            @event.Tier,
            @event.Title,
            @event.Description,
            @event.BannerImageUrl,
            @event.CategoryId,
            categoryName,
            @event.StartTime,
            @event.EndTime,
            @event.TimeZoneId,
            @event.AdmissionType,
            @event.Status,
            @event.LocationType,
            editAccess.HostParticipantId,
            editAccess.HostIsGroup,
            @event.CreatedByUserId,
            @event.CreatedAt,
            @event.PublishedAt,
            @event.Locations.Select(ToLocationResponse).ToList(),
            plugins ?? [],
            rsvpSummary);

    public static PublicEventResponse ToPublicResponse(
        Event @event,
        string hostDisplayName,
        bool hostIsGroup,
        string? categoryName,
        IReadOnlyList<EventPluginResponse>? plugins = null,
        EventRsvpSummaryResponse? rsvpSummary = null) =>
        new(
            @event.Tier,
            @event.Title,
            @event.Description,
            @event.BannerImageUrl,
            @event.CategoryId,
            categoryName,
            @event.StartTime,
            @event.EndTime,
            @event.TimeZoneId,
            @event.AdmissionType,
            @event.Status,
            @event.LocationType,
            @event.PublishedAt,
            hostDisplayName,
            hostIsGroup,
            @event.Locations.Select(ToLocationResponse).ToList(),
            plugins ?? [],
            rsvpSummary);

    public static EventLocation ToDomainLocation(EventLocationResponse location) =>
        new()
        {
            Name = location.Name,
            StartsAt = location.StartsAt,
            EndsAt = location.EndsAt,
            Kind = location.Kind,
            Url = location.Url,
            Address = location.Address,
            Latitude = location.Latitude,
            Longitude = location.Longitude,
            City = location.City,
            Country = location.Country,
            ExternalPlaceId = location.ExternalPlaceId
        };

    private static EventLocationResponse ToLocationResponse(EventLocation location) =>
        new(
            location.Name,
            location.StartsAt,
            location.EndsAt,
            location.Kind,
            location.Url,
            location.Address,
            location.Latitude,
            location.Longitude,
            location.City,
            location.Country,
            location.ExternalPlaceId);
}
