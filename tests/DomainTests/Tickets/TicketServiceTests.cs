using Domain.Tickets;
using Domain.Tickets.Services;
using System.Text.RegularExpressions;

namespace DomainTests.Tickets;

public class TicketServiceTests
{
    private static readonly Regex ManualCodePattern = new("^[A-Z0-9]{8}$");

    [Fact]
    public void TicketService_Should_IssueTicketsWithCodes_When_OrderIsPaid()
    {
        // Arrange
        var @event = TicketTestData.MakePaidPublished();
        var (order, ticketType, _) = TicketTestData.CreatePaidOrderWithTickets(@event, quantity: 3);

        // Act
        var tickets = order.Tickets;

        // Assert
        Assert.Equal(3, tickets.Count);
        Assert.All(tickets, ticket =>
        {
            Assert.Equal(TicketTestData.BuyerParticipantId, ticket.ParticipantId);
            Assert.NotNull(ticket.TicketCode);
            Assert.Matches(ManualCodePattern, ticket.TicketCode!.ManualCode);
        });
    }

    [Fact]
    public void TicketService_Should_ReturnOrderNotPaid_When_IssuingForPendingOrder()
    {
        // Arrange
        var @event = TicketTestData.MakePaidPublished();
        var (order, ticketType) = TicketTestData.CreatePendingOrder(@event, quantity: 1);

        // Act
        var result = TicketService.IssueTickets(
            order,
            ticketType,
            TicketTestData.BuyerParticipantId,
            quantity: 1);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(TicketErrors.OrderNotPaid.Code, result.Error.Code);
    }

    [Fact]
    public void TicketService_Should_GenerateManualCodeWithExpectedLengthAndCharset()
    {
        // Act
        var code = TicketService.GenerateManualCode();

        // Assert
        Assert.Equal(TicketConstants.ManualCodeLength, code.Length);
        Assert.Matches(ManualCodePattern, code);
    }

    [Fact]
    public void TicketService_Should_RegenerateManualCode_When_CodeIsAlreadyTaken()
    {
        // Arrange
        var @event = TicketTestData.MakePaidPublished();
        var (order, ticketType) = TicketTestData.CreatePendingOrder(@event, quantity: 1);
        OrderService.MarkPaid(order, ticketType, quantity: 1);

        var firstGenerated = true;

        // Act
        var result = TicketService.IssueTickets(
            order,
            ticketType,
            TicketTestData.BuyerParticipantId,
            quantity: 1,
            isManualCodeTaken: _ =>
            {
                if (firstGenerated)
                {
                    firstGenerated = false;
                    return true;
                }

                return false;
            });

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Matches(ManualCodePattern, result.Value[0].TicketCode!.ManualCode);
    }
}
