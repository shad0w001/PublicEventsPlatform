using Application.Tickets;
using Application.Tickets.GetTicket;
using Domain.Tickets;

namespace ApplicationTests.Tickets;

public class GetTicketQueryHandlerTests
{
    [Fact]
    public async Task GetTicketQueryHandler_Should_ExposeCodes_When_TicketActive()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = TicketQueryTestHelper.CreateVerifiedIdentity("auth0|ticket-active", "active@example.com");
        var seed = await TicketQueryTestHelper.SeedPaidOrderWithTicketsAsync(databaseName, identity, quantity: 1);
        await using var context = TicketQueryTestHelper.CreateContext(databaseName);
        var handler = TicketQueryTestHelper.CreateGetTicketHandler(context, identity);

        // Act
        var result = await handler.Handle(new GetTicketQuery(seed.TicketIds[0]), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(TicketDisplayState.Active, result.Value.State);
        Assert.Equal(seed.ManualCode, result.Value.ManualCode);
        Assert.Equal(seed.TicketIds[0].ToString(), result.Value.QrPayload);
    }

    [Fact]
    public async Task GetTicketQueryHandler_Should_HideCodes_When_TicketUsed()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = TicketQueryTestHelper.CreateVerifiedIdentity("auth0|ticket-used", "used@example.com");
        var seed = await TicketQueryTestHelper.SeedPaidOrderWithTicketsAsync(databaseName, identity, quantity: 1);
        await TicketQueryTestHelper.MarkTicketCheckedInAsync(
            databaseName,
            seed.TicketIds[0],
            seed.BuyerParticipantId);
        await using var context = TicketQueryTestHelper.CreateContext(databaseName);
        var handler = TicketQueryTestHelper.CreateGetTicketHandler(context, identity);

        // Act
        var result = await handler.Handle(new GetTicketQuery(seed.TicketIds[0]), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(TicketDisplayState.Used, result.Value.State);
        Assert.Null(result.Value.ManualCode);
        Assert.Null(result.Value.QrPayload);
    }

    [Fact]
    public async Task GetTicketQueryHandler_Should_ReturnForbidden_When_NotOwner()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = TicketQueryTestHelper.CreateVerifiedIdentity("auth0|ticket-owner", "owner@example.com");
        var stranger = TicketQueryTestHelper.CreateVerifiedIdentity("auth0|ticket-stranger", "stranger@example.com");
        var seed = await TicketQueryTestHelper.SeedPaidOrderWithTicketsAsync(databaseName, owner, quantity: 1);
        await using var context = TicketQueryTestHelper.CreateContext(databaseName);
        var handler = TicketQueryTestHelper.CreateGetTicketHandler(context, stranger);

        // Act
        var result = await handler.Handle(new GetTicketQuery(seed.TicketIds[0]), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(TicketErrors.InsufficientPurchasePermissions.Code, result.Error.Code);
    }

    [Fact]
    public async Task GetTicketQueryHandler_Should_AllowOrganizerPlus_When_GroupTicket()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = TicketQueryTestHelper.CreateVerifiedIdentity("auth0|ticket-grp-owner", "grp-owner@example.com");
        var organizer = TicketQueryTestHelper.CreateVerifiedIdentity("auth0|ticket-grp-org", "grp-org@example.com");
        var (seed, _) = await TicketQueryTestHelper.SeedGroupPaidOrderAsync(databaseName, owner, organizer);
        await using var context = TicketQueryTestHelper.CreateContext(databaseName);
        var handler = TicketQueryTestHelper.CreateGetTicketHandler(context, organizer);

        // Act
        var result = await handler.Handle(new GetTicketQuery(seed.TicketIds[0]), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(TicketDisplayState.Active, result.Value.State);
    }
}
