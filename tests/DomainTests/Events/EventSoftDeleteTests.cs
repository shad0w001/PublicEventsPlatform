using Domain.Events;
using Domain.Events.Events;
using Domain.Events.Services;

namespace DomainTests.Events;

public class EventSoftDeleteTests
{
    [Fact]
    public void EventService_Should_SetDeletedAt_When_DraftSoftDeleted()
    {
        // Arrange
        var (eventEntity, _) = EventTestData.CreateDraft();

        // Act
        var result = EventService.SoftDelete(eventEntity);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(eventEntity.DeletedAt);
        Assert.True(eventEntity.IsDeleted);
        Assert.Contains(eventEntity.DomainEvents, e => e is EventSoftDeleted);
    }

    [Fact]
    public void EventService_Should_BeIdempotent_When_AlreadyDeleted()
    {
        // Arrange
        var (eventEntity, _) = EventTestData.CreateDraft();
        EventService.SoftDelete(eventEntity);
        var firstDeletedAt = eventEntity.DeletedAt;
        eventEntity.ClearDomainEvents();

        // Act
        var result = EventService.SoftDelete(eventEntity);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(firstDeletedAt, eventEntity.DeletedAt);
        Assert.Empty(eventEntity.DomainEvents);
    }

    [Fact]
    public void EventService_Should_ReturnNotDraft_When_PublishedEventDeleted()
    {
        // Arrange
        var (eventEntity, _) = EventTestData.CreateDraft();
        EventTestData.MakePublishReady(eventEntity);
        EventService.Publish(eventEntity, categoryExists: true, recentPublishCount: 0, maxPublishesPerWeek: 6);

        // Act
        var result = EventService.SoftDelete(eventEntity);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.NotDraft", result.Error.Code);
    }
}
