using Domain.Events;
using Domain.Events.Services;
using Domain.Tickets;
using Domain.Tickets.Events;
using Domain.Tickets.Services;

namespace DomainTests.Tickets;

public class OrderServiceTests
{
    [Fact]
    public void OrderService_Should_CreatePendingOrder_When_EventIsPublishedPaidAndQuantityAvailable()
    {
        // Arrange
        var @event = TicketTestData.MakePaidPublished();
        var ticketType = TicketTestData.CreateTicketType(@event, capacity: 10);

        // Act
        var result = OrderService.CreatePending(
            @event,
            ticketType,
            TicketTestData.BuyerParticipantId,
            quantity: 2,
            TicketTestData.DefaultExpiresAt);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Pending, result.Value.Status);
        Assert.Equal(2, result.Value.Quantity);
    }

    [Fact]
    public void OrderService_Should_ReturnInvalidQuantity_When_QuantityExceedsRemaining()
    {
        // Arrange
        var @event = TicketTestData.MakePaidPublished();
        var ticketType = TicketTestData.CreateTicketType(@event, capacity: 5);

        // Act
        var result = OrderService.CreatePending(
            @event,
            ticketType,
            TicketTestData.BuyerParticipantId,
            quantity: 6,
            TicketTestData.DefaultExpiresAt);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(TicketErrors.InvalidQuantity.Code, result.Error.Code);
    }

    [Fact]
    public void OrderService_Should_ReturnInsufficientInventory_When_ReserveExceedsRemaining()
    {
        // Arrange
        var @event = TicketTestData.MakePaidPublished();
        var ticketType = TicketTestData.CreateTicketType(@event, capacity: 3);

        // Act
        var result = OrderService.ReserveInventory(ticketType, quantity: 4);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(TicketErrors.InsufficientInventory.Code, result.Error.Code);
    }

    [Fact]
    public void OrderService_Should_ReserveAndReleaseInventory_When_PendingOrderExpires()
    {
        // Arrange
        var @event = TicketTestData.MakePaidPublished();
        var (order, ticketType) = TicketTestData.CreatePendingOrder(@event, quantity: 2);

        // Act
        var expireResult = OrderService.MarkExpired(order, ticketType, quantity: 2);

        // Assert
        Assert.True(expireResult.IsSuccess);
        Assert.Equal(OrderStatus.Expired, order.Status);
        Assert.Equal(0, ticketType.ReservedQuantity);
        Assert.Equal(100, TicketTypeService.GetRemainingQuantity(ticketType));
    }

    [Fact]
    public void OrderService_Should_MoveReservedToSold_When_OrderIsPaid()
    {
        // Arrange
        var @event = TicketTestData.MakePaidPublished();
        var (order, ticketType) = TicketTestData.CreatePendingOrder(@event, quantity: 3);

        // Act
        var result = OrderService.MarkPaid(order, ticketType, quantity: 3);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.Equal(3, ticketType.SoldQuantity);
        Assert.Equal(0, ticketType.ReservedQuantity);
        Assert.Contains(order.DomainEvents, e => e is TicketPurchaseCompleted);
    }

    [Fact]
    public void OrderService_Should_ReturnPaidAdmissionRequired_When_EventIsFree()
    {
        // Arrange
        var @event = DomainTests.Events.EventTestData.MakePublished();
        var ticketType = new TicketType
        {
            EventId = @event.Id,
            Capacity = 10,
            Event = @event
        };

        // Act
        var result = OrderService.CreatePending(
            @event,
            ticketType,
            TicketTestData.BuyerParticipantId,
            quantity: 1,
            TicketTestData.DefaultExpiresAt);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(TicketErrors.PaidAdmissionRequired.Code, result.Error.Code);
    }

    [Fact]
    public void OrderService_Should_AttachCheckoutSession_When_OrderIsPending()
    {
        // Arrange
        var @event = TicketTestData.MakePaidPublished();
        var (order, _) = TicketTestData.CreatePendingOrder(@event, quantity: 1);

        // Act
        var result = OrderService.AttachCheckoutSession(order, "cs_test_123", "pi_test_456");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("cs_test_123", order.CheckoutSessionId);
        Assert.Equal("pi_test_456", order.PaymentIntentId);
    }

    [Fact]
    public void OrderService_Should_ReturnOrderNotPending_When_AttachingCheckoutSessionToPaidOrder()
    {
        // Arrange
        var @event = TicketTestData.MakePaidPublished();
        var (order, ticketType) = TicketTestData.CreatePendingOrder(@event, quantity: 1);
        OrderService.MarkPaid(order, ticketType, quantity: 1);

        // Act
        var result = OrderService.AttachCheckoutSession(order, "cs_test_123", null);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(TicketErrors.OrderNotPending.Code, result.Error.Code);
    }

    [Fact]
    public void OrderService_Should_ReturnEventCancelled_When_EventIsCancelled()
    {
        // Arrange
        var @event = TicketTestData.MakePaidPublished();
        EventService.Cancel(@event);
        var ticketType = TicketTestData.CreateTicketType(@event);

        // Act
        var result = OrderService.CreatePending(
            @event,
            ticketType,
            TicketTestData.BuyerParticipantId,
            quantity: 1,
            TicketTestData.DefaultExpiresAt);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(TicketErrors.EventCancelled.Code, result.Error.Code);
    }
}
