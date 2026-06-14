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
    }
}
