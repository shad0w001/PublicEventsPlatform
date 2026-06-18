using Domain.Events;
using Domain.Events.Events;
using Domain.Events.Services;

namespace DomainTests.Events;

public class EventAttendeeServiceTests
{
    private static readonly Guid AttendeeParticipantId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly DateTime UtcNow = new(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void SetRsvpStatus_Should_AddGoingAttendeeWithRegisteredAt_When_NewRsvpOnPublishedFreeEvent()
    {
        // Arrange
        var eventEntity = EventTestData.MakePublished();

        // Act
        var result = EventAttendeeService.SetRsvpStatus(
            eventEntity,
            AttendeeParticipantId,
            EventTestData.HostParticipantId,
            EventAttendeeStatus.Going,
            UtcNow);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(eventEntity.Attendees);
        Assert.Equal(EventAttendeeStatus.Going, result.Value.Status);
        Assert.Equal(UtcNow, result.Value.RegisteredAt);
    }

    [Fact]
    public void SetRsvpStatus_Should_AddInterestedAttendeeWithRegisteredAt_When_NewRsvpOnPublishedFreeEvent()
    {
        // Arrange
        var eventEntity = EventTestData.MakePublished();

        // Act
        var result = EventAttendeeService.SetRsvpStatus(
            eventEntity,
            AttendeeParticipantId,
            EventTestData.HostParticipantId,
            EventAttendeeStatus.Interested,
            UtcNow);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(eventEntity.Attendees);
        Assert.Equal(EventAttendeeStatus.Interested, result.Value.Status);
        Assert.Equal(UtcNow, result.Value.RegisteredAt);
    }

    [Fact]
    public void SetRsvpStatus_Should_PreserveRegisteredAt_When_GoingChangesToNotGoing()
    {
        // Arrange
        var eventEntity = EventTestData.MakePublished();
        EventAttendeeService.SetRsvpStatus(
            eventEntity,
            AttendeeParticipantId,
            EventTestData.HostParticipantId,
            EventAttendeeStatus.Going,
            UtcNow);

        // Act
        var result = EventAttendeeService.SetRsvpStatus(
            eventEntity,
            AttendeeParticipantId,
            EventTestData.HostParticipantId,
            EventAttendeeStatus.NotGoing,
            UtcNow.AddHours(1));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(eventEntity.Attendees);
        Assert.Equal(EventAttendeeStatus.NotGoing, result.Value.Status);
        Assert.Equal(UtcNow, result.Value.RegisteredAt);
    }

    [Fact]
    public void SetRsvpStatus_Should_Succeed_When_StatusIsUnchanged()
    {
        // Arrange
        var eventEntity = EventTestData.MakePublished();
        EventAttendeeService.SetRsvpStatus(
            eventEntity,
            AttendeeParticipantId,
            EventTestData.HostParticipantId,
            EventAttendeeStatus.Going,
            UtcNow);

        // Act
        var result = EventAttendeeService.SetRsvpStatus(
            eventEntity,
            AttendeeParticipantId,
            EventTestData.HostParticipantId,
            EventAttendeeStatus.Going,
            UtcNow.AddHours(1));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(eventEntity.Attendees);
        Assert.Equal(EventAttendeeStatus.Going, result.Value.Status);
        Assert.Equal(UtcNow, result.Value.RegisteredAt);
    }

    [Fact]
    public void SetRsvpStatus_Should_ReturnNotPublished_When_EventIsDraft()
    {
        // Arrange
        var (eventEntity, _) = EventTestData.CreateDraft();
        EventTestData.MakePublishReady(eventEntity);

        // Act
        var result = EventAttendeeService.SetRsvpStatus(
            eventEntity,
            AttendeeParticipantId,
            EventTestData.HostParticipantId,
            EventAttendeeStatus.Going,
            UtcNow);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(EventErrors.NotPublished.Code, result.Error.Code);
    }

    [Fact]
    public void SetRsvpStatus_Should_ReturnPaidAdmissionNotAllowed_When_EventIsPaid()
    {
        // Arrange
        var eventEntity = EventTestData.MakePublished();
        eventEntity.AdmissionType = AdmissionType.Paid;

        // Act
        var result = EventAttendeeService.SetRsvpStatus(
            eventEntity,
            AttendeeParticipantId,
            EventTestData.HostParticipantId,
            EventAttendeeStatus.Going,
            UtcNow);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(EventAttendeeErrors.PaidAdmissionNotAllowed.Code, result.Error.Code);
    }

    [Fact]
    public void SetRsvpStatus_Should_ReturnCannotModifyCancelled_When_EventIsCancelled()
    {
        // Arrange
        var eventEntity = EventTestData.MakePublished();
        EventService.Cancel(eventEntity);

        // Act
        var result = EventAttendeeService.SetRsvpStatus(
            eventEntity,
            AttendeeParticipantId,
            EventTestData.HostParticipantId,
            EventAttendeeStatus.Going,
            UtcNow);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(EventErrors.CannotModifyCancelled.Code, result.Error.Code);
    }

    [Fact]
    public void SetRsvpStatus_Should_ReturnDeleted_When_EventIsDeleted()
    {
        // Arrange
        var eventEntity = EventTestData.MakePublished();
        eventEntity.DeletedAt = UtcNow;

        // Act
        var result = EventAttendeeService.SetRsvpStatus(
            eventEntity,
            AttendeeParticipantId,
            EventTestData.HostParticipantId,
            EventAttendeeStatus.Going,
            UtcNow);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(EventErrors.Deleted(eventEntity.Id).Code, result.Error.Code);
    }

    [Fact]
    public void SetRsvpStatus_Should_ReturnHostMustRemainGoing_When_HostSetsNotGoing()
    {
        // Arrange
        var eventEntity = EventTestData.MakePublished();

        // Act
        var result = EventAttendeeService.SetRsvpStatus(
            eventEntity,
            EventTestData.HostParticipantId,
            EventTestData.HostParticipantId,
            EventAttendeeStatus.NotGoing,
            UtcNow);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(EventAttendeeErrors.HostMustRemainGoing.Code, result.Error.Code);
    }

    [Fact]
    public void SetRsvpStatus_Should_ReturnHostMustRemainGoing_When_HostSetsInterested()
    {
        // Arrange
        var eventEntity = EventTestData.MakePublished();

        // Act
        var result = EventAttendeeService.SetRsvpStatus(
            eventEntity,
            EventTestData.HostParticipantId,
            EventTestData.HostParticipantId,
            EventAttendeeStatus.Interested,
            UtcNow);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(EventAttendeeErrors.HostMustRemainGoing.Code, result.Error.Code);
    }

    [Fact]
    public void SetRsvpStatus_Should_RaiseEventRsvpStatusChanged_When_StatusChanges()
    {
        // Arrange
        var eventEntity = EventTestData.MakePublished();
        eventEntity.ClearDomainEvents();

        // Act
        var result = EventAttendeeService.SetRsvpStatus(
            eventEntity,
            AttendeeParticipantId,
            EventTestData.HostParticipantId,
            EventAttendeeStatus.Going,
            UtcNow);

        // Assert
        Assert.True(result.IsSuccess);
        var domainEvent = Assert.Single(eventEntity.DomainEvents.OfType<EventRsvpStatusChanged>());
        Assert.Equal(eventEntity.Id, domainEvent.EventId);
        Assert.Equal(AttendeeParticipantId, domainEvent.ParticipantId);
        Assert.Equal(EventAttendeeStatus.Going, domainEvent.Status);
        Assert.Null(domainEvent.PreviousStatus);
    }

    [Fact]
    public void SetRsvpStatus_Should_NotRaiseEventRsvpStatusChanged_When_StatusIsUnchanged()
    {
        // Arrange
        var eventEntity = EventTestData.MakePublished();
        EventAttendeeService.SetRsvpStatus(
            eventEntity,
            AttendeeParticipantId,
            EventTestData.HostParticipantId,
            EventAttendeeStatus.Going,
            UtcNow);
        eventEntity.ClearDomainEvents();

        // Act
        var result = EventAttendeeService.SetRsvpStatus(
            eventEntity,
            AttendeeParticipantId,
            EventTestData.HostParticipantId,
            EventAttendeeStatus.Going,
            UtcNow.AddHours(1));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(eventEntity.DomainEvents);
    }

    [Fact]
    public void EnsureHostGoing_Should_AddHostGoingAttendee_When_FreePublishedEvent()
    {
        // Arrange
        var eventEntity = EventTestData.MakePublished();

        // Act
        var result = EventAttendeeService.EnsureHostGoing(eventEntity, UtcNow);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(EventTestData.HostParticipantId, result.Value.ParticipantId);
        Assert.Equal(EventAttendeeStatus.Going, result.Value.Status);
        Assert.Equal(UtcNow, result.Value.RegisteredAt);
        Assert.Single(eventEntity.Attendees);
    }

    [Fact]
    public void EnsureHostGoing_Should_RemainIdempotent_When_CalledTwice()
    {
        // Arrange
        var eventEntity = EventTestData.MakePublished();
        EventAttendeeService.EnsureHostGoing(eventEntity, UtcNow);

        // Act
        var result = EventAttendeeService.EnsureHostGoing(eventEntity, UtcNow.AddHours(1));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Single(eventEntity.Attendees);
        Assert.Equal(EventAttendeeStatus.Going, result.Value.Status);
        Assert.Equal(UtcNow, result.Value.RegisteredAt);
    }

    [Fact]
    public void EnsureHostGoing_Should_NotAddAttendee_When_EventIsPaid()
    {
        // Arrange
        var eventEntity = EventTestData.MakePublished();
        eventEntity.AdmissionType = AdmissionType.Paid;

        // Act
        var result = EventAttendeeService.EnsureHostGoing(eventEntity, UtcNow);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Empty(eventEntity.Attendees);
    }

    [Fact]
    public void CountRsvps_Should_ExcludeNotGoing_When_MixedStatusesExist()
    {
        // Arrange
        var attendees = new List<EventAttendee>
        {
            new() { Status = EventAttendeeStatus.Going },
            new() { Status = EventAttendeeStatus.Going },
            new() { Status = EventAttendeeStatus.Interested },
            new() { Status = EventAttendeeStatus.NotGoing }
        };

        // Act
        var (going, interested, responseCount) = EventAttendeeService.CountRsvps(attendees);

        // Assert
        Assert.Equal(2, going);
        Assert.Equal(1, interested);
        Assert.Equal(3, responseCount);
    }
}
