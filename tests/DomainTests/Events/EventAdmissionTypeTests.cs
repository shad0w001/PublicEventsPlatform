using Domain.Events;
using Domain.Events.Services;
using Domain.Tickets.Services;

namespace DomainTests.Events;

public class EventAdmissionTypeTests
{
    private static readonly Guid AttendeeParticipantId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    [Fact]
    public void ApplyAdmissionTypeChange_Should_Succeed_When_SameAdmissionType()
    {
        // Arrange
        var (draft, _) = EventTestData.CreateDraft();
        EventTestData.MakePublishReady(draft);

        // Act
        var result = EventService.ApplyAdmissionTypeChange(
            draft,
            AdmissionType.Free,
            draft.TicketTypes);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ApplyAdmissionTypeChange_Should_ReturnAdmissionTypeImmutable_When_EventIsPublished()
    {
        // Arrange
        var published = EventTestData.MakePublished();

        // Act
        var result = EventService.ApplyAdmissionTypeChange(
            published,
            AdmissionType.Paid,
            published.TicketTypes);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(EventErrors.AdmissionTypeImmutable.Code, result.Error.Code);
    }

    [Fact]
    public void ApplyAdmissionTypeChange_Should_ReturnCannotSwitchToPaidWithRsvps_When_DraftHasAttendees()
    {
        // Arrange
        var (draft, _) = EventTestData.CreateDraft();
        EventTestData.MakePublishReady(draft);
        var attendee = EventAttendee.Create(draft.Id, AttendeeParticipantId);
        attendee.Status = EventAttendeeStatus.Going;
        draft.Attendees.Add(attendee);

        // Act
        var result = EventService.ApplyAdmissionTypeChange(
            draft,
            AdmissionType.Paid,
            draft.TicketTypes);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(EventErrors.CannotSwitchToPaidWithRsvps.Code, result.Error.Code);
    }

    [Fact]
    public void ApplyAdmissionTypeChange_Should_ClearTicketTypes_When_DraftPaidToFreeWithoutSales()
    {
        // Arrange
        var (draft, _) = EventTestData.CreateDraft();
        draft.AdmissionType = AdmissionType.Paid;
        TicketTypeService.Create(draft, "General", "Entry", priceCents: 2500, capacity: 100);

        // Act
        var result = EventService.ApplyAdmissionTypeChange(
            draft,
            AdmissionType.Free,
            draft.TicketTypes);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(draft.TicketTypes);
    }

    [Fact]
    public void ApplyAdmissionTypeChange_Should_ReturnCannotSwitchToFreeWithTicketSales_When_SoldQuantityPositive()
    {
        // Arrange
        var (draft, _) = EventTestData.CreateDraft();
        draft.AdmissionType = AdmissionType.Paid;
        var ticketType = TicketTypeService.Create(draft, "General", "Entry", priceCents: 2500, capacity: 100).Value;
        ticketType.SoldQuantity = 1;

        // Act
        var result = EventService.ApplyAdmissionTypeChange(
            draft,
            AdmissionType.Free,
            draft.TicketTypes);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(EventErrors.CannotSwitchToFreeWithTicketSales.Code, result.Error.Code);
        Assert.Single(draft.TicketTypes);
    }

    [Fact]
    public void ApplyAdmissionTypeChange_Should_AllowFirstSet_When_AdmissionTypeWasNull()
    {
        // Arrange
        var (draft, _) = EventTestData.CreateDraft();

        // Act
        var result = EventService.ApplyAdmissionTypeChange(
            draft,
            AdmissionType.Paid,
            draft.TicketTypes);

        // Assert
        Assert.True(result.IsSuccess);
    }
}
