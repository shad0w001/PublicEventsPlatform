using Domain.Events.EventLocations;

namespace Application.Events;

public sealed record EventLocationResponse(
    string Name,
    DateTime Date,
    EventLocationKind Kind,
    string? Url,
    string? Address,
    double? Latitude,
    double? Longitude,
    string? City,
    string? Country,
    string? ExternalPlaceId);
