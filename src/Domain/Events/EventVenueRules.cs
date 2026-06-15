using System.Text.RegularExpressions;
using Domain.Events.EventLocations;
using Domain.Events.Services;

namespace Domain.Events;

public static class EventVenueRules
{
    public sealed record PublishedEventOccupancy(
        Guid EventId,
        DateTime StartTime,
        DateTime EndTime,
        IReadOnlyList<EventLocation> Locations);

    public static bool PhysicalPlacesMatch(EventLocation a, EventLocation b)
    {
        if (a.Kind != EventLocationKind.Physical || b.Kind != EventLocationKind.Physical)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(a.ExternalPlaceId) &&
            !string.IsNullOrWhiteSpace(b.ExternalPlaceId))
        {
            return string.Equals(
                a.ExternalPlaceId.Trim(),
                b.ExternalPlaceId.Trim(),
                StringComparison.Ordinal);
        }

        var addressA = NormalizePlaceComponent(a.Address);
        var cityA = NormalizePlaceComponent(a.City);
        var addressB = NormalizePlaceComponent(b.Address);
        var cityB = NormalizePlaceComponent(b.City);

        if (addressA is not null && cityA is not null && addressB is not null && cityB is not null &&
            addressA == addressB && cityA == cityB)
        {
            return true;
        }

        if (a.Latitude.HasValue && a.Longitude.HasValue &&
            b.Latitude.HasValue && b.Longitude.HasValue)
        {
            return RoundCoordinate(a.Latitude.Value) == RoundCoordinate(b.Latitude.Value) &&
                   RoundCoordinate(a.Longitude.Value) == RoundCoordinate(b.Longitude.Value);
        }

        return false;
    }

    public static bool TimeRangesOverlap(DateTime startA, DateTime endA, DateTime startB, DateTime endB) =>
        startA < endB && startB < endA;

    public static bool HasVenueConflict(
        Event sourceEvent,
        IReadOnlyList<PublishedEventOccupancy> occupants)
    {
        foreach (var segment in sourceEvent.Locations)
        {
            if (segment.Kind != EventLocationKind.Physical)
            {
                continue;
            }

            var (sourceStart, sourceEnd) = EventService.GetEffectiveSegmentWindow(segment, sourceEvent);

            foreach (var occupant in occupants)
            {
                if (occupant.EventId == sourceEvent.Id)
                {
                    continue;
                }

                foreach (var otherSegment in occupant.Locations)
                {
                    if (otherSegment.Kind != EventLocationKind.Physical)
                    {
                        continue;
                    }

                    if (!PhysicalPlacesMatch(segment, otherSegment))
                    {
                        continue;
                    }

                    var (otherStart, otherEnd) = EventService.GetEffectiveSegmentWindow(
                        otherSegment,
                        occupant.StartTime,
                        occupant.EndTime);

                    if (TimeRangesOverlap(sourceStart, sourceEnd, otherStart, otherEnd))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    public static string? NormalizePlaceKey(string? address, string? city)
    {
        var normalizedAddress = NormalizePlaceComponent(address);
        var normalizedCity = NormalizePlaceComponent(city);

        if (normalizedAddress is null || normalizedCity is null)
        {
            return null;
        }

        return $"{normalizedAddress}|{normalizedCity}";
    }

    internal static string? NormalizePlaceComponent(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var collapsed = Regex.Replace(value.Trim(), @"\s+", " ");
        return collapsed.ToLowerInvariant();
    }

    private static double RoundCoordinate(double value) =>
        Math.Round(value, 6, MidpointRounding.AwayFromZero);
}
