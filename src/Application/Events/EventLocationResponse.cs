using Domain.Events.EventLocations;

namespace Application.Events;

public sealed record EventLocationResponse(
    string Name,
    DateTime? StartsAt,
    DateTime? EndsAt,
    EventLocationKind Kind,
    string? Url,
    string? Address,
    double? Latitude,
    double? Longitude,
    string? City,
    string? Country,
    string? ExternalPlaceId);
