using Domain.Events;
using Domain.Events.EventLocations;
using Domain.Events.Events;
using Domain.Events.Services;
using DomainTests.Tickets;

namespace DomainTests.Events;

public class EventPublishTests
{
    private const int MaxPublishesPerWeek = 6;

    [Fact]
    public void EventService_Should_Publish_When_EventIsComplete()
    {
        // Arrange
        var (eventEntity, _) = EventTestData.CreateDraft();
        EventTestData.MakePublishReady(eventEntity);
        eventEntity.ClearDomainEvents();

        // Act
        var result = EventService.Publish(
            eventEntity,
            categoryExists: true,
            recentPublishCount: 0,
            maxPublishesPerWeek: MaxPublishesPerWeek);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(EventStatus.Published, eventEntity.Status);
        Assert.NotNull(eventEntity.PublishedAt);
        Assert.Equal(EventLocationType.Physical, eventEntity.LocationType);
        Assert.Contains(eventEntity.DomainEvents, e => e is EventPublished);
    }

    [Fact]
    public void EventService_Should_DeriveHybridLocationType_When_MixedSegmentsExist()
    {
        // Arrange
        var (eventEntity, _) = EventTestData.CreateDraft();
        EventTestData.MakePublishReady(eventEntity);
        eventEntity.Locations.Add(EventTestData.VirtualLocation());

        // Act
        var result = EventService.Publish(
            eventEntity,
            categoryExists: true,
            recentPublishCount: 0,
            maxPublishesPerWeek: MaxPublishesPerWeek);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(EventLocationType.Hybrid, eventEntity.LocationType);
    }

    [Fact]
    public void EventService_Should_ReturnInvalidTitle_When_TitleIsEmpty()
    {
        // Arrange
        var (eventEntity, _) = EventTestData.CreateDraft();
        EventTestData.MakePublishReady(eventEntity);
        eventEntity.Title = "   ";

        // Act
        var result = EventService.Publish(
            eventEntity,
            categoryExists: true,
            recentPublishCount: 0,
            maxPublishesPerWeek: MaxPublishesPerWeek);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.InvalidTitle", result.Error.Code);
    }

    [Fact]
    public void EventService_Should_ReturnCategoryRequired_When_CategoryMissing()
    {
        // Arrange
        var (eventEntity, _) = EventTestData.CreateDraft();
        EventTestData.MakePublishReady(eventEntity);
        eventEntity.CategoryId = null;

        // Act
        var result = EventService.Publish(
            eventEntity,
            categoryExists: true,
            recentPublishCount: 0,
            maxPublishesPerWeek: MaxPublishesPerWeek);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.CategoryRequired", result.Error.Code);
    }

    [Fact]
    public void EventService_Should_ReturnInvalidTimeZoneId_When_TimeZoneNotSet()
    {
        // Arrange
        var (eventEntity, _) = EventTestData.CreateDraft();
        EventTestData.MakePublishReady(eventEntity);
        eventEntity.TimeZoneId = null;

        // Act
        var result = EventService.Publish(
            eventEntity,
            categoryExists: true,
            recentPublishCount: 0,
            maxPublishesPerWeek: MaxPublishesPerWeek);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.InvalidTimeZoneId", result.Error.Code);
    }

    [Fact]
    public void EventService_Should_ReturnAdmissionTypeRequired_When_AdmissionNotSet()
    {
        // Arrange
        var (eventEntity, _) = EventTestData.CreateDraft();
        EventTestData.MakePublishReady(eventEntity);
        eventEntity.AdmissionType = null;

        // Act
        var result = EventService.Publish(
            eventEntity,
            categoryExists: true,
            recentPublishCount: 0,
            maxPublishesPerWeek: MaxPublishesPerWeek);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.AdmissionTypeRequired", result.Error.Code);
    }

    [Fact]
    public void EventService_Should_ReturnInvalidTimeRange_When_EndBeforeStart()
    {
        // Arrange
        var (eventEntity, _) = EventTestData.CreateDraft();
        EventTestData.MakePublishReady(eventEntity);
        eventEntity.EndTime = eventEntity.StartTime.AddHours(-1);

        // Act
        var result = EventService.Publish(
            eventEntity,
            categoryExists: true,
            recentPublishCount: 0,
            maxPublishesPerWeek: MaxPublishesPerWeek);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.InvalidTimeRange", result.Error.Code);
    }

    [Fact]
    public void EventService_Should_ReturnTooManyLocationsForSmallTier_When_ThreeSegments()
    {
        // Arrange
        var (eventEntity, _) = EventTestData.CreateDraft(EventTier.Small);
        EventTestData.MakePublishReady(eventEntity);
        eventEntity.Locations.Add(EventTestData.PhysicalLocation("Second"));
        eventEntity.Locations.Add(EventTestData.VirtualLocation("Third"));

        // Act
        var result = EventService.Publish(
            eventEntity,
            categoryExists: true,
            recentPublishCount: 0,
            maxPublishesPerWeek: MaxPublishesPerWeek);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.TooManyLocationsForSmallTier", result.Error.Code);
    }

    [Fact]
    public void EventService_Should_Publish_When_SmallTierHasTwoLocations()
    {
        // Arrange
        var (eventEntity, _) = EventTestData.CreateDraft(EventTier.Small);
        EventTestData.MakePublishReady(eventEntity);
        eventEntity.Locations.Add(EventTestData.VirtualLocation("Stream"));

        // Act
        var result = EventService.Publish(
            eventEntity,
            categoryExists: true,
            recentPublishCount: 0,
            maxPublishesPerWeek: MaxPublishesPerWeek);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(EventLocationType.Hybrid, eventEntity.LocationType);
    }

    [Fact]
    public void EventService_Should_Publish_When_BigTierHasManyLocations()
    {
        // Arrange
        var (eventEntity, _) = EventTestData.CreateDraft(EventTier.Big);
        EventTestData.MakePublishReady(eventEntity);
        eventEntity.Locations.Add(EventTestData.PhysicalLocation("Day 2"));
        eventEntity.Locations.Add(EventTestData.VirtualLocation("Stream"));

        // Act
        var result = EventService.Publish(
            eventEntity,
            categoryExists: true,
            recentPublishCount: 0,
            maxPublishesPerWeek: MaxPublishesPerWeek);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(3, eventEntity.Locations.Count);
    }

    [Fact]
    public void EventService_Should_ReturnPaidPublishRequiresTicketTypes_When_PaidDraftHasNoTicketTypes()
    {
        // Arrange
        var (eventEntity, _) = EventTestData.CreateDraft();
        EventTestData.MakePublishReady(eventEntity);
        eventEntity.AdmissionType = AdmissionType.Paid;

        // Act
        var result = EventService.Publish(
            eventEntity,
            categoryExists: true,
            recentPublishCount: 0,
            maxPublishesPerWeek: MaxPublishesPerWeek);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(EventErrors.PaidPublishRequiresTicketTypes.Code, result.Error.Code);
    }

    [Fact]
    public void EventService_Should_PublishPaidEvent_When_TicketTypeExists()
    {
        // Arrange
        var (eventEntity, _) = EventTestData.CreateDraft();
        EventTestData.MakePublishReady(eventEntity);
        eventEntity.AdmissionType = AdmissionType.Paid;
        TicketTestData.CreateTicketType(eventEntity);

        // Act
        var result = EventService.Publish(
            eventEntity,
            categoryExists: true,
            recentPublishCount: 0,
            maxPublishesPerWeek: MaxPublishesPerWeek);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(AdmissionType.Paid, eventEntity.AdmissionType);
    }
}
