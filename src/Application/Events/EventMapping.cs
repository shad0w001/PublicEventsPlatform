using Application.Events.Services;
using Domain.Events;
using Domain.Events.EventLocations;

namespace Application.Events;

internal static class EventMapping
{
    public static EventDetailResponse ToDetailResponse(Event @event, EventEditAccess editAccess) =>
        new(
            @event.Id,
            @event.Tier,
            @event.Title,
            @event.Description,
            @event.BannerImageUrl,
            @event.CategoryId,
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
            @event.Locations.Select(ToLocationResponse).ToList());

    public static EventLocation ToDomainLocation(EventLocationResponse location) =>
        new()
        {
            Name = location.Name,
            Date = location.Date,
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
            location.Date,
            location.Kind,
            location.Url,
            location.Address,
            location.Latitude,
            location.Longitude,
            location.City,
            location.Country,
            location.ExternalPlaceId);
}
