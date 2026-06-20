using Domain.Events;
using Domain.Tickets;
using Domain.Tickets.Services;
using DomainTests.Events;

namespace DomainTests.Tickets;

public class TicketValidationServiceTests
{
    private static readonly DateTime DuringEvent = EventTestData.DefaultEventStart.AddHours(1);

    [Fact]
    public void TicketValidationService_Should_ReturnValid_When_FirstQrScanDuringEventWindow()
    {
        // Arrange
        var @event = TicketTestData.MakePaidPublished();
        var ticket = TicketTestData.CreateTicketWithCode(@event);

        // Act
        var result = TicketValidationService.Validate(
            ticket,
            @event,
            validations: [],
            code: ticket.Id.ToString(),
            TicketValidationMethod.QrScan,
            TicketTestData.ValidatorUserId,
            DuringEvent);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(TicketValidationStatus.Valid, result.Value.Status);
        Assert.Single(ticket.Validations);
    }

    [Fact]
    public void TicketValidationService_Should_ReturnAlreadyUsed_When_ValidTicketScannedAgain()
    {
        // Arrange
        var @event = TicketTestData.MakePaidPublished();
        var ticket = TicketTestData.CreateTicketWithCode(@event);
        var first = TicketValidationService.Validate(
            ticket,
            @event,
            validations: [],
            code: ticket.Id.ToString(),
            TicketValidationMethod.QrScan,
            TicketTestData.ValidatorUserId,
            DuringEvent).Value;

        // Act
        var result = TicketValidationService.Validate(
            ticket,
            @event,
            validations: [first],
            code: ticket.Id.ToString(),
            TicketValidationMethod.QrScan,
            TicketTestData.ValidatorUserId,
            DuringEvent);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(TicketValidationStatus.AlreadyUsed, result.Value.Status);
    }

    [Fact]
    public void TicketValidationService_Should_ReturnInvalid_When_ManualCodeDoesNotMatch()
    {
        // Arrange
        var @event = TicketTestData.MakePaidPublished();
        var ticket = TicketTestData.CreateTicketWithCode(@event, manualCode: "AB12CD34");

        // Act
        var result = TicketValidationService.Validate(
            ticket,
            @event,
            validations: [],
            code: "WRONG123",
            TicketValidationMethod.ManualEntry,
            TicketTestData.ValidatorUserId,
            DuringEvent);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(TicketValidationStatus.Invalid, result.Value.Status);
    }

    [Fact]
    public void TicketValidationService_Should_AcceptManualCodeCaseInsensitively_When_CodeMatches()
    {
        // Arrange
        var @event = TicketTestData.MakePaidPublished();
        var ticket = TicketTestData.CreateTicketWithCode(@event, manualCode: "AB12CD34");

        // Act
        var result = TicketValidationService.Validate(
            ticket,
            @event,
            validations: [],
            code: "ab12cd34",
            TicketValidationMethod.ManualEntry,
            TicketTestData.ValidatorUserId,
            DuringEvent);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(TicketValidationStatus.Valid, result.Value.Status);
    }

    [Fact]
    public void TicketValidationService_Should_ReturnInvalid_When_OutsideEventWindow()
    {
        // Arrange
        var @event = TicketTestData.MakePaidPublished();
        var ticket = TicketTestData.CreateTicketWithCode(@event);

        // Act
        var result = TicketValidationService.Validate(
            ticket,
            @event,
            validations: [],
            code: ticket.Id.ToString(),
            TicketValidationMethod.QrScan,
            TicketTestData.ValidatorUserId,
            EventTestData.DefaultEventStart.AddHours(-1));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(TicketValidationStatus.Invalid, result.Value.Status);
    }

    [Fact]
    public void TicketValidationService_Should_ReturnInvalid_When_EventIsCancelled()
    {
        // Arrange
        var @event = TicketTestData.MakePaidPublished();
        var ticket = TicketTestData.CreateTicketWithCode(@event);
        @event.Status = EventStatus.Cancelled;

        // Act
        var result = TicketValidationService.Validate(
            ticket,
            @event,
            validations: [],
            code: ticket.Id.ToString(),
            TicketValidationMethod.QrScan,
            TicketTestData.ValidatorUserId,
            DuringEvent);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(TicketValidationStatus.Invalid, result.Value.Status);
    }
}
