using Domain.Events;
using Domain.Events.EventLocations;
using Domain.Events.Services;

namespace DomainTests.Events;

public class EventVenueRulesTests
{
    private static readonly DateTime WindowStart = new(2026, 7, 1, 18, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime WindowEnd = new(2026, 7, 1, 22, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void PhysicalPlacesMatch_Should_ReturnTrue_When_ExternalPlaceIdsEqual()
    {
        // Arrange
        var a = PhysicalSegment(externalPlaceId: "ChIJabc123");
        var b = PhysicalSegment(externalPlaceId: "ChIJabc123", address: "Different St");

        // Act
        var result = EventVenueRules.PhysicalPlacesMatch(a, b);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void PhysicalPlacesMatch_Should_ReturnFalse_When_ExternalPlaceIdsDiffer()
    {
        // Arrange
        var a = PhysicalSegment(externalPlaceId: "ChIJabc123");
        var b = PhysicalSegment(externalPlaceId: "ChIJxyz999");

        // Act
        var result = EventVenueRules.PhysicalPlacesMatch(a, b);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void PhysicalPlacesMatch_Should_ReturnTrue_When_NormalizedAddressAndCityEqual()
    {
        // Arrange
        var a = PhysicalSegment(address: "  123 Main St  ", city: "Sofia");
        var b = PhysicalSegment(address: "123 main st", city: "SOFIA");

        // Act
        var result = EventVenueRules.PhysicalPlacesMatch(a, b);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void PhysicalPlacesMatch_Should_ReturnTrue_When_CoordinatesMatchRounded()
    {
        // Arrange
        var a = PhysicalSegment(latitude: 42.6977004, longitude: 23.3219001);
        var b = PhysicalSegment(latitude: 42.6977005, longitude: 23.3219004);

        // Act
        var result = EventVenueRules.PhysicalPlacesMatch(a, b);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void PhysicalPlacesMatch_Should_ReturnFalse_When_PlacesDiffer()
    {
        // Arrange
        var a = PhysicalSegment(address: "123 Main St", city: "Sofia");
        var b = PhysicalSegment(address: "456 Other St", city: "Plovdiv");

        // Act
        var result = EventVenueRules.PhysicalPlacesMatch(a, b);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void PhysicalPlacesMatch_Should_ReturnFalse_When_VirtualSegmentInvolved()
    {
        // Arrange
        var physical = PhysicalSegment();
        var virtualSegment = EventTestData.VirtualLocation();

        // Act
        var result = EventVenueRules.PhysicalPlacesMatch(physical, virtualSegment);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void TimeRangesOverlap_Should_ReturnTrue_When_RangesPartiallyOverlap()
    {
        // Arrange
        var startA = WindowStart;
        var endA = WindowStart.AddHours(3);
        var startB = WindowStart.AddHours(2);
        var endB = WindowEnd;

        // Act
        var result = EventVenueRules.TimeRangesOverlap(startA, endA, startB, endB);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void TimeRangesOverlap_Should_ReturnFalse_When_RangesAreAdjacent()
    {
        // Arrange
        var startA = WindowStart;
        var endA = WindowStart.AddHours(2);
        var startB = WindowStart.AddHours(2);
        var endB = WindowEnd;

        // Act
        var result = EventVenueRules.TimeRangesOverlap(startA, endA, startB, endB);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void HasVenueConflict_Should_ReturnTrue_When_PublishedEventSharesVenueAndOverlappingWindow()
    {
        // Arrange
        var source = CreateEventWithPhysicalVenue();
        var occupant = CreateOccupancy(
            Guid.NewGuid(),
            WindowStart,
            WindowEnd,
            PhysicalSegment());

        // Act
        var result = EventVenueRules.HasVenueConflict(source, [occupant]);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasVenueConflict_Should_ReturnFalse_When_OnlyVirtualSegmentsOverlap()
    {
        // Arrange
        var source = CreateEventWithPhysicalVenue();
        var occupant = CreateOccupancy(
            Guid.NewGuid(),
            WindowStart,
            WindowEnd,
            EventTestData.VirtualLocation());

        // Act
        var result = EventVenueRules.HasVenueConflict(source, [occupant]);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void HasVenueConflict_Should_ReturnFalse_When_SameEventIdExcluded()
    {
        // Arrange
        var source = CreateEventWithPhysicalVenue();
        var occupant = CreateOccupancy(
            source.Id,
            WindowStart,
            WindowEnd,
            PhysicalSegment());

        // Act
        var result = EventVenueRules.HasVenueConflict(source, [occupant]);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void HasVenueConflict_Should_UseSegmentTimes_When_BothSet()
    {
        // Arrange
        var source = CreateEventWithPhysicalVenue();
        source.Locations[0].StartsAt = WindowStart.AddHours(4);
        source.Locations[0].EndsAt = WindowEnd;

        var occupant = CreateOccupancy(
            Guid.NewGuid(),
            WindowStart,
            WindowEnd,
            PhysicalSegment(
                startsAt: WindowStart,
                endsAt: WindowStart.AddHours(3)));

        // Act
        var result = EventVenueRules.HasVenueConflict(source, [occupant]);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void NormalizePlaceKey_Should_CollapseWhitespaceAndLowercase()
    {
        // Arrange
        // Act
        var key = EventVenueRules.NormalizePlaceKey("  123 Main   St ", " SOFIA ");

        // Assert
        Assert.Equal("123 main st|sofia", key);
    }

    private static Event CreateEventWithPhysicalVenue()
    {
        var (eventEntity, _) = EventTestData.CreateDraft();
        EventTestData.MakePublishReady(eventEntity);
        return eventEntity;
    }

    private static EventVenueRules.PublishedEventOccupancy CreateOccupancy(
        Guid eventId,
        DateTime start,
        DateTime end,
        EventLocation location) =>
        new(eventId, start, end, [location]);

    private static EventLocation PhysicalSegment(
        string? externalPlaceId = null,
        string address = "123 Main St",
        string city = "Sofia",
        double? latitude = null,
        double? longitude = null,
        DateTime? startsAt = null,
        DateTime? endsAt = null) =>
        new()
        {
            Name = "Main Hall",
            Kind = EventLocationKind.Physical,
            ExternalPlaceId = externalPlaceId,
            Address = address,
            City = city,
            Latitude = latitude,
            Longitude = longitude,
            StartsAt = startsAt,
            EndsAt = endsAt
        };
}
