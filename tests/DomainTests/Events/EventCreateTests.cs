using Domain.Events;
using Domain.Events.Events;
using Domain.Events.Services;

namespace DomainTests.Events;

public class EventCreateTests
{
    [Fact]
    public void EventService_Should_CreateDraftWithOrganizer_When_InputIsValid()
    {
        // Arrange
        var hostId = EventTestData.HostParticipantId;
        const string title = "Summer Meetup";

        // Act
        var result = EventService.Create(EventTier.Small, title, hostId, EventTestData.DefaultBannerUrl);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(EventTier.Small, result.Value.Event.Tier);
        Assert.Equal(EventStatus.Draft, result.Value.Event.Status);
        Assert.Equal("Summer Meetup", result.Value.Event.Title);
        Assert.Equal(EventTestData.DefaultBannerUrl, result.Value.Event.BannerImageUrl);
        Assert.Equal(EventConstants.DraftEpochUtc, result.Value.Event.StartTime);
        Assert.Null(result.Value.Event.CreatedByUserId);
        Assert.Equal(hostId, result.Value.Organizer.ParticipantId);
        Assert.Single(result.Value.Event.Organizers);
    }

    [Fact]
    public void EventService_Should_RaiseEventCreated_When_Created()
    {
        // Arrange
        var hostId = EventTestData.HostParticipantId;

        // Act
        var result = EventService.Create(EventTier.Big, EventTestData.DefaultTitle, hostId, EventTestData.DefaultBannerUrl);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Contains(
            result.Value.Event.DomainEvents,
            e => e is EventCreated created &&
                 created.EventId == result.Value.Event.Id &&
                 created.HostParticipantId == hostId &&
                 created.Tier == EventTier.Big);
    }

    [Fact]
    public void EventService_Should_ReturnInvalidTitle_When_TitleIsEmpty()
    {
        // Arrange
        var hostId = EventTestData.HostParticipantId;

        // Act
        var result = EventService.Create(EventTier.Small, "   ", hostId, EventTestData.DefaultBannerUrl);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.InvalidTitle", result.Error.Code);
    }
}
