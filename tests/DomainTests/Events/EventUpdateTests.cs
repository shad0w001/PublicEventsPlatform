using Domain.Events;
using Domain.Events.Events;
using Domain.Events.Services;

namespace DomainTests.Events;

public class EventUpdateTests
{
    [Fact]
    public void EventService_Should_SetCreatedByUserId_When_FirstPatchApplied()
    {
        // Arrange
        var (eventEntity, _) = EventTestData.CreateDraft();
        var actingUserId = EventTestData.ActingUserId;
        var patch = new EventUpdatePatch { Title = "My Event" };

        // Act
        var result = EventService.Update(eventEntity, patch, actingUserId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(actingUserId, eventEntity.CreatedByUserId);
        Assert.Equal("My Event", eventEntity.Title);
    }

    [Fact]
    public void EventService_Should_RaiseEventUpdated_When_PatchApplied()
    {
        // Arrange
        var (eventEntity, _) = EventTestData.CreateDraft();
        eventEntity.ClearDomainEvents();
        var patch = new EventUpdatePatch { Description = "Updated description" };

        // Act
        var result = EventService.Update(eventEntity, patch, EventTestData.ActingUserId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Contains(eventEntity.DomainEvents, e => e is EventUpdated);
    }

    [Fact]
    public void EventService_Should_ReturnNoFieldsToUpdate_When_PatchIsEmpty()
    {
        // Arrange
        var (eventEntity, _) = EventTestData.CreateDraft();

        // Act
        var result = EventService.Update(eventEntity, new EventUpdatePatch(), EventTestData.ActingUserId);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.NoFieldsToUpdate", result.Error.Code);
    }

    [Fact]
    public void EventService_Should_ReturnCannotModifyCancelled_When_EventIsCancelled()
    {
        // Arrange
        var (eventEntity, _) = EventTestData.CreateDraft();
        EventTestData.MakePublishReady(eventEntity);
        EventService.Publish(eventEntity, categoryExists: true, recentPublishCount: 0, maxPublishesPerWeek: 6);
        EventService.Cancel(eventEntity);
        eventEntity.ClearDomainEvents();

        // Act
        var result = EventService.Update(
            eventEntity,
            new EventUpdatePatch { Title = "Nope" },
            EventTestData.ActingUserId);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.CannotModifyCancelled", result.Error.Code);
    }

    [Fact]
    public void EventService_Should_ReturnCannotModifyDeleted_When_EventIsSoftDeleted()
    {
        // Arrange
        var (eventEntity, _) = EventTestData.CreateDraft();
        EventService.SoftDelete(eventEntity);

        // Act
        var result = EventService.Update(
            eventEntity,
            new EventUpdatePatch { Title = "Nope" },
            EventTestData.ActingUserId);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.CannotModifyDeleted", result.Error.Code);
    }

    [Fact]
    public void EventService_Should_ReplaceLocations_When_LocationsProvided()
    {
        // Arrange
        var (eventEntity, _) = EventTestData.CreateDraft();
        var patch = new EventUpdatePatch
        {
            Locations = [EventTestData.PhysicalLocation("Hall A"), EventTestData.VirtualLocation("Live")]
        };

        // Act
        var result = EventService.Update(eventEntity, patch, EventTestData.ActingUserId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, eventEntity.Locations.Count);
        Assert.Equal(EventLocationType.Hybrid, eventEntity.LocationType);
    }

    [Fact]
    public void EventService_Should_ReturnTooManyLocationsForSmallTier_When_ThreeLocationsOnSmallTier()
    {
        // Arrange
        var (eventEntity, _) = EventTestData.CreateDraft(EventTier.Small);
        var patch = new EventUpdatePatch
        {
            Locations =
            [
                EventTestData.PhysicalLocation("A"),
                EventTestData.PhysicalLocation("B"),
                EventTestData.PhysicalLocation("C")
            ]
        };

        // Act
        var result = EventService.Update(eventEntity, patch, EventTestData.ActingUserId);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.TooManyLocationsForSmallTier", result.Error.Code);
    }

    [Fact]
    public void EventService_Should_ReturnInvalidLocationSegment_When_PhysicalSegmentMissingAddressAndCity()
    {
        // Arrange
        var (eventEntity, _) = EventTestData.CreateDraft();
        var patch = new EventUpdatePatch
        {
            Locations =
            [
                new Domain.Events.EventLocations.EventLocation
                {
                    Name = "Bad Venue",
                    Date = new DateTime(2026, 7, 1, 18, 0, 0, DateTimeKind.Utc),
                    Kind = Domain.Events.EventLocations.EventLocationKind.Physical
                }
            ]
        };

        // Act
        var result = EventService.Update(eventEntity, patch, EventTestData.ActingUserId);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.InvalidLocationSegment", result.Error.Code);
    }

    [Fact]
    public void EventService_Should_ReturnInvalidTimeRange_When_BothTimesSetAndStartNotBeforeEnd()
    {
        // Arrange
        var (eventEntity, _) = EventTestData.CreateDraft();
        var start = new DateTime(2026, 7, 1, 22, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 7, 1, 18, 0, 0, DateTimeKind.Utc);
        var patch = new EventUpdatePatch { StartTime = start, EndTime = end };

        // Act
        var result = EventService.Update(eventEntity, patch, EventTestData.ActingUserId);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.InvalidTimeRange", result.Error.Code);
    }

    [Fact]
    public void EventService_Should_ReturnInvalidTimeZoneId_When_TimeZoneIdIsInvalid()
    {
        // Arrange
        var (eventEntity, _) = EventTestData.CreateDraft();
        var patch = new EventUpdatePatch { TimeZoneId = "Not/A/Real/Zone" };

        // Act
        var result = EventService.Update(eventEntity, patch, EventTestData.ActingUserId);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.InvalidTimeZoneId", result.Error.Code);
    }

    [Fact]
    public void EventService_Should_ReturnCannotDowngradeTier_When_PublishedEventTierDowngraded()
    {
        // Arrange
        var (eventEntity, _) = EventTestData.CreateDraft(EventTier.Big);
        EventTestData.MakePublishReady(eventEntity);
        EventService.Publish(eventEntity, categoryExists: true, recentPublishCount: 0, maxPublishesPerWeek: 6);
        var patch = new EventUpdatePatch { Tier = EventTier.Small };

        // Act
        var result = EventService.Update(eventEntity, patch, EventTestData.ActingUserId);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.CannotDowngradeTier", result.Error.Code);
    }

    [Fact]
    public void EventService_Should_DeriveHybridLocationType_When_MixedSegmentsPatched()
    {
        // Arrange
        var (eventEntity, _) = EventTestData.CreateDraft();
        var patch = new EventUpdatePatch
        {
            Locations = [EventTestData.PhysicalLocation("Hall"), EventTestData.VirtualLocation("Stream")]
        };

        // Act
        var result = EventService.Update(eventEntity, patch, EventTestData.ActingUserId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(EventLocationType.Hybrid, eventEntity.LocationType);
    }
}
