using Domain.Events;
using Domain.Events.EventLocations;
using Domain.Events.Services;

namespace DomainTests.Events;

public class EventSegmentTimeTests
{
    [Fact]
    public void EventService_Should_AllowNullSegmentTimes_When_Publishing()
    {
        // Arrange
        var (eventEntity, _) = EventTestData.CreateDraft();
        EventTestData.MakePublishReady(eventEntity);

        // Act
        var result = EventService.Publish(
            eventEntity,
            categoryExists: true,
            recentPublishCount: 0,
            maxPublishesPerWeek: 6);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(eventEntity.Locations[0].StartsAt);
        Assert.Null(eventEntity.Locations[0].EndsAt);
    }

    [Fact]
    public void EventService_Should_AllowSegmentWithinBounds_When_BothSegmentTimesSet()
    {
        // Arrange
        var (eventEntity, _) = EventTestData.CreateDraft();
        EventTestData.MakePublishReady(eventEntity);
        eventEntity.Locations[0].StartsAt = EventTestData.DefaultEventStart;
        eventEntity.Locations[0].EndsAt = EventTestData.DefaultEventEnd;

        // Act
        var result = EventService.Publish(
            eventEntity,
            categoryExists: true,
            recentPublishCount: 0,
            maxPublishesPerWeek: 6);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void EventService_Should_ReturnSegmentTimeOutOfBounds_When_SegmentExceedsEventWindow()
    {
        // Arrange
        var (eventEntity, _) = EventTestData.CreateDraft();
        var patch = new EventUpdatePatch
        {
            StartTime = EventTestData.DefaultEventStart,
            EndTime = EventTestData.DefaultEventEnd,
            Locations =
            [
                EventTestData.PhysicalLocation(
                    startsAt: EventTestData.DefaultEventStart.AddHours(-1),
                    endsAt: EventTestData.DefaultEventEnd)
            ]
        };

        // Act
        var result = EventService.Update(eventEntity, patch, EventTestData.ActingUserId);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.SegmentTimeOutOfBounds", result.Error.Code);
    }

    [Fact]
    public void EventService_Should_ReturnSegmentTimesIncomplete_When_OnlyStartsAtSet()
    {
        // Arrange
        var (eventEntity, _) = EventTestData.CreateDraft();
        var patch = new EventUpdatePatch
        {
            Locations =
            [
                EventTestData.PhysicalLocation(startsAt: EventTestData.DefaultEventStart)
            ]
        };

        // Act
        var result = EventService.Update(eventEntity, patch, EventTestData.ActingUserId);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.SegmentTimesIncomplete", result.Error.Code);
    }

    [Fact]
    public void EventService_Should_ReturnInvalidSegmentTimeRange_When_StartsAtNotBeforeEndsAt()
    {
        // Arrange
        var (eventEntity, _) = EventTestData.CreateDraft();
        var patch = new EventUpdatePatch
        {
            StartTime = EventTestData.DefaultEventStart,
            EndTime = EventTestData.DefaultEventEnd,
            Locations =
            [
                EventTestData.PhysicalLocation(
                    startsAt: EventTestData.DefaultEventEnd,
                    endsAt: EventTestData.DefaultEventStart)
            ]
        };

        // Act
        var result = EventService.Update(eventEntity, patch, EventTestData.ActingUserId);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.InvalidSegmentTimeRange", result.Error.Code);
    }

    [Fact]
    public void EventService_Should_RejectEventTimePatch_When_ExistingSegmentsFallOutsideNewBounds()
    {
        // Arrange
        var (eventEntity, _) = EventTestData.CreateDraft();
        EventService.Update(
            eventEntity,
            new EventUpdatePatch
            {
                StartTime = EventTestData.DefaultEventStart,
                EndTime = EventTestData.DefaultEventEnd,
                Locations =
                [
                    EventTestData.PhysicalLocation(
                        startsAt: EventTestData.DefaultEventStart,
                        endsAt: EventTestData.DefaultEventEnd)
                ]
            },
            EventTestData.ActingUserId);

        var patch = new EventUpdatePatch
        {
            StartTime = EventTestData.DefaultEventStart.AddHours(2),
            EndTime = EventTestData.DefaultEventEnd
        };

        // Act
        var result = EventService.Update(eventEntity, patch, EventTestData.ActingUserId);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.SegmentTimeOutOfBounds", result.Error.Code);
    }

    [Fact]
    public void EventService_Should_AcceptPhysicalSegment_When_OnlyCoordinatesProvided()
    {
        // Arrange
        var (eventEntity, _) = EventTestData.CreateDraft();
        var patch = new EventUpdatePatch
        {
            Locations = [EventTestData.PhysicalLocationWithCoordinates()]
        };

        // Act
        var result = EventService.Update(eventEntity, patch, EventTestData.ActingUserId);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void GetEffectiveSegmentWindow_Should_UseEventTimes_When_SegmentTimesNull()
    {
        // Arrange
        var (eventEntity, _) = EventTestData.CreateDraft();
        EventTestData.MakePublishReady(eventEntity);
        var location = eventEntity.Locations[0];

        // Act
        var window = EventService.GetEffectiveSegmentWindow(location, eventEntity);

        // Assert
        Assert.Equal(eventEntity.StartTime, window.Start);
        Assert.Equal(eventEntity.EndTime, window.End);
    }

    [Fact]
    public void GetEffectiveSegmentWindow_Should_UseSegmentTimes_When_BothSet()
    {
        // Arrange
        var (eventEntity, _) = EventTestData.CreateDraft();
        EventTestData.MakePublishReady(eventEntity);
        var segmentStart = EventTestData.DefaultEventStart.AddHours(1);
        var segmentEnd = EventTestData.DefaultEventEnd.AddHours(-1);
        var location = new EventLocation
        {
            Name = "Hall",
            Kind = EventLocationKind.Physical,
            Address = "1 Main St",
            City = "Sofia",
            StartsAt = segmentStart,
            EndsAt = segmentEnd
        };

        // Act
        var window = EventService.GetEffectiveSegmentWindow(location, eventEntity);

        // Assert
        Assert.Equal(segmentStart, window.Start);
        Assert.Equal(segmentEnd, window.End);
    }
}
