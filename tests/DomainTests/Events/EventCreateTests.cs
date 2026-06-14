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

        // Act
        var result = EventService.Create(EventTier.Small, hostId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(EventTier.Small, result.Value.Event.Tier);
        Assert.Equal(EventStatus.Draft, result.Value.Event.Status);
        Assert.Equal(string.Empty, result.Value.Event.Title);
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
        var result = EventService.Create(EventTier.Big, hostId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Contains(
            result.Value.Event.DomainEvents,
            e => e is EventCreated created &&
                 created.EventId == result.Value.Event.Id &&
                 created.HostParticipantId == hostId &&
                 created.Tier == EventTier.Big);
    }
}
