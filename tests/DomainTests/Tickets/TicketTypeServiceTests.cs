using Domain.Tickets;
using Domain.Tickets.Services;

namespace DomainTests.Tickets;

public class TicketTypeServiceTests
{
    [Fact]
    public void TicketTypeService_Should_CreateTicketType_When_PriceAndCapacityAreValid()
    {
        // Arrange
        var @event = TicketTestData.MakePaidDraft();

        // Act
        var result = TicketTypeService.Create(
            @event,
            "VIP",
            "VIP access",
            priceCents: 5000,
            capacity: 50);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("VIP", result.Value.Name);
        Assert.Equal(5000, result.Value.PriceCents);
        Assert.Equal(50, result.Value.Capacity);
        Assert.Single(@event.TicketTypes);
    }

    [Fact]
    public void TicketTypeService_Should_ReturnPriceMustBePositive_When_PriceIsZero()
    {
        // Arrange
        var @event = TicketTestData.MakePaidPublished();

        // Act
        var result = TicketTypeService.Create(@event, "Free?", null, priceCents: 0, capacity: 10);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(TicketErrors.PriceMustBePositive.Code, result.Error.Code);
    }

    [Fact]
    public void TicketTypeService_Should_ReturnCapacityTooLow_When_CapacityBelowSoldPlusReserved()
    {
        // Arrange
        var @event = TicketTestData.MakePaidPublished();
        var ticketType = TicketTestData.CreateTicketType(@event, capacity: 20);
        ticketType.SoldQuantity = 5;
        ticketType.ReservedQuantity = 3;

        // Act
        var result = TicketTypeService.Update(ticketType, name: null, description: null, priceCents: null, capacity: 7);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(TicketErrors.CapacityTooLow(8).Code, result.Error.Code);
    }

    [Fact]
    public void TicketTypeService_Should_UpdateCapacity_When_NewCapacityCoversSoldAndReserved()
    {
        // Arrange
        var @event = TicketTestData.MakePaidPublished();
        var ticketType = TicketTestData.CreateTicketType(@event, capacity: 20);
        ticketType.SoldQuantity = 5;
        ticketType.ReservedQuantity = 3;

        // Act
        var result = TicketTypeService.Update(ticketType, name: null, description: null, priceCents: null, capacity: 10);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(10, ticketType.Capacity);
    }

    [Fact]
    public void TicketTypeService_Should_ReturnCannotDeleteWithSales_When_SoldQuantityIsPositive()
    {
        // Arrange
        var @event = TicketTestData.MakePaidPublished();
        var ticketType = TicketTestData.CreateTicketType(@event);
        ticketType.SoldQuantity = 1;

        // Act
        var result = TicketTypeService.Delete(ticketType);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(TicketErrors.CannotDeleteWithSales.Code, result.Error.Code);
    }

    [Fact]
    public void TicketTypeService_Should_DeleteTicketType_When_NoSalesOrReservations()
    {
        // Arrange
        var @event = TicketTestData.MakePaidDraft();
        var ticketType = TicketTestData.CreateTicketType(@event);

        // Act
        var result = TicketTypeService.Delete(ticketType);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(@event.TicketTypes);
    }

    [Fact]
    public void TicketTypeService_Should_ReturnRemainingQuantity_When_InventoryIsPartiallyUsed()
    {
        // Arrange
        var @event = TicketTestData.MakePaidPublished();
        var ticketType = TicketTestData.CreateTicketType(@event, capacity: 100);
        ticketType.SoldQuantity = 30;
        ticketType.ReservedQuantity = 10;

        // Act
        var remaining = TicketTypeService.GetRemainingQuantity(ticketType);

        // Assert
        Assert.Equal(60, remaining);
    }

    [Fact]
    public void TicketTypeService_Should_ReturnPaidAdmissionRequired_When_EventIsFree()
    {
        // Arrange
        var (draft, _) = DomainTests.Events.EventTestData.CreateDraft();
        DomainTests.Events.EventTestData.MakePublishReady(draft);

        // Act
        var result = TicketTypeService.Create(draft, "General", null, priceCents: 2500, capacity: 100);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(TicketErrors.PaidAdmissionRequired.Code, result.Error.Code);
    }

    [Fact]
    public void TicketTypeService_Should_UpdatePrice_When_NoSalesExist()
    {
        // Arrange
        var @event = TicketTestData.MakePaidPublished();
        var ticketType = TicketTestData.CreateTicketType(@event, priceCents: 2500);

        // Act
        var result = TicketTypeService.Update(ticketType, name: null, description: null, priceCents: 3000, capacity: null);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(3000, ticketType.PriceCents);
    }

    [Fact]
    public void TicketTypeService_Should_ReturnPriceImmutableAfterSales_When_PriceChangedAfterSale()
    {
        // Arrange
        var @event = TicketTestData.MakePaidPublished();
        var ticketType = TicketTestData.CreateTicketType(@event);
        ticketType.SoldQuantity = 1;

        // Act
        var result = TicketTypeService.Update(ticketType, name: null, description: null, priceCents: 3000, capacity: null);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(TicketErrors.PriceImmutableAfterSales.Code, result.Error.Code);
    }
}
