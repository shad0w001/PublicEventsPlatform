using Application.Tickets;
using Application.Tickets.GetOrder;
using Domain.Tickets;
using Microsoft.EntityFrameworkCore;

namespace ApplicationTests.Tickets;

public class GetOrderQueryHandlerTests
{
    [Fact]
    public async Task GetOrderQueryHandler_Should_ReturnConfirmation_When_PaidOrderAndBuyer()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = TicketQueryTestHelper.CreateVerifiedIdentity("auth0|order-read", "order-read@example.com");
        var seed = await TicketQueryTestHelper.SeedPaidOrderWithTicketsAsync(databaseName, identity);
        await using var context = TicketQueryTestHelper.CreateContext(databaseName);
        var handler = TicketQueryTestHelper.CreateGetOrderHandler(context, identity);

        // Act
        var result = await handler.Handle(new GetOrderQuery(seed.OrderId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(seed.OrderId, result.Value.OrderId);
        Assert.Equal(OrderStatus.Paid, result.Value.Status);
        Assert.Equal(2, result.Value.Quantity);
        Assert.Equal(2, result.Value.TicketIds.Count);
        Assert.False(result.Value.RefundPending);
        Assert.Equal(seed.EventId, result.Value.Event.EventId);
    }

    [Fact]
    public async Task GetOrderQueryHandler_Should_ReturnRefundPending_When_EventCancelled()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = TicketQueryTestHelper.CreateVerifiedIdentity("auth0|order-cancel-read", "order-cancel@example.com");
        var seed = await TicketQueryTestHelper.SeedPaidOrderWithTicketsAsync(
            databaseName,
            identity,
            cancelledEvent: true);
        await using var context = TicketQueryTestHelper.CreateContext(databaseName);
        var handler = TicketQueryTestHelper.CreateGetOrderHandler(context, identity);

        // Act
        var result = await handler.Handle(new GetOrderQuery(seed.OrderId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Value.RefundPending);
    }

    [Fact]
    public async Task GetOrderQueryHandler_Should_ReturnForbidden_When_NotBuyer()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var buyer = TicketQueryTestHelper.CreateVerifiedIdentity("auth0|order-owner", "owner@example.com");
        var other = TicketQueryTestHelper.CreateVerifiedIdentity("auth0|order-stranger", "stranger@example.com");
        var seed = await TicketQueryTestHelper.SeedPaidOrderWithTicketsAsync(databaseName, buyer);
        await using var context = TicketQueryTestHelper.CreateContext(databaseName);
        var handler = TicketQueryTestHelper.CreateGetOrderHandler(context, other);

        // Act
        var result = await handler.Handle(new GetOrderQuery(seed.OrderId), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(TicketErrors.InsufficientPurchasePermissions.Code, result.Error.Code);
    }

    [Fact]
    public async Task GetOrderQueryHandler_Should_ReturnOrderNotPaid_When_OrderPending()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = TicketQueryTestHelper.CreateVerifiedIdentity("auth0|order-pending", "pending@example.com");
        var seed = await TicketQueryTestHelper.SeedPaidOrderWithTicketsAsync(databaseName, identity);
        await using var context = TicketQueryTestHelper.CreateContext(databaseName);
        var order = await context.Orders.SingleAsync(o => o.Id == seed.OrderId);
        order.Status = OrderStatus.Pending;
        await context.SaveChangesAsync(CancellationToken.None);
        var handler = TicketQueryTestHelper.CreateGetOrderHandler(context, identity);

        // Act
        var result = await handler.Handle(new GetOrderQuery(seed.OrderId), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(TicketErrors.OrderNotPaid.Code, result.Error.Code);
    }
}
