using Domain.Events;
using Domain.Events.Events;
using Domain.Events.Services;

namespace DomainTests.Events;

public class EventCancelTests
{
    [Fact]
    public void EventService_Should_Cancel_When_EventIsPublished()
    {
        // Arrange
        var (eventEntity, _) = EventTestData.CreateDraft();
        EventTestData.MakePublishReady(eventEntity);
        EventService.Publish(eventEntity, categoryExists: true, recentPublishCount: 0, maxPublishesPerWeek: 6);
        eventEntity.ClearDomainEvents();

        // Act
        var result = EventService.Cancel(eventEntity);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(EventStatus.Cancelled, eventEntity.Status);
        Assert.Contains(eventEntity.DomainEvents, e => e is EventCancelled);
    }

    [Fact]
    public void EventService_Should_ReturnNotPublished_When_EventIsDraft()
    {
        // Arrange
        var (eventEntity, _) = EventTestData.CreateDraft();

        // Act
        var result = EventService.Cancel(eventEntity);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.NotPublished", result.Error.Code);
    }
}
