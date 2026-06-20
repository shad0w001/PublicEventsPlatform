using Application.Tickets.ListMyTickets;
using Domain.Tickets;

namespace ApplicationTests.Tickets;

public class ListMyTicketsQueryHandlerTests
{
    [Fact]
    public async Task ListMyTicketsQueryHandler_Should_ReturnPersonalTickets_When_UserPurchased()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = TicketQueryTestHelper.CreateVerifiedIdentity("auth0|list-personal", "personal@example.com");
        var seed = await TicketQueryTestHelper.SeedPaidOrderWithTicketsAsync(databaseName, identity);
        await using var context = TicketQueryTestHelper.CreateContext(databaseName);
        var handler = TicketQueryTestHelper.CreateListMyTicketsHandler(context, identity);

        // Act
        var result = await handler.Handle(new ListMyTicketsQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.PersonalTickets.Count);
        Assert.Empty(result.Value.GroupTickets);
        Assert.All(result.Value.PersonalTickets, t => Assert.Equal(seed.EventId, t.EventId));
        Assert.All(result.Value.PersonalTickets, t => Assert.False(t.IsCheckedIn));
    }

    [Fact]
    public async Task ListMyTicketsQueryHandler_Should_ReturnGroupTickets_When_OrganizerPlusOnBuyerGroup()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = TicketQueryTestHelper.CreateVerifiedIdentity("auth0|list-group-owner", "owner@example.com");
        var organizer = TicketQueryTestHelper.CreateVerifiedIdentity("auth0|list-group-org", "org@example.com");
        var (seed, _) = await TicketQueryTestHelper.SeedGroupPaidOrderAsync(databaseName, owner, organizer);
        await using var context = TicketQueryTestHelper.CreateContext(databaseName);
        var handler = TicketQueryTestHelper.CreateListMyTicketsHandler(context, organizer);

        // Act
        var result = await handler.Handle(new ListMyTicketsQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.PersonalTickets);
        Assert.Single(result.Value.GroupTickets);
        Assert.True(result.Value.GroupTickets[0].ParticipantIsGroup);
        Assert.Equal(seed.TicketIds[0], result.Value.GroupTickets[0].TicketId);
    }

    [Fact]
    public async Task ListMyTicketsQueryHandler_Should_SetIsCheckedIn_When_TicketValidated()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = TicketQueryTestHelper.CreateVerifiedIdentity("auth0|list-checked", "checked@example.com");
        var seed = await TicketQueryTestHelper.SeedPaidOrderWithTicketsAsync(databaseName, identity, quantity: 1);
        await TicketQueryTestHelper.MarkTicketCheckedInAsync(
            databaseName,
            seed.TicketIds[0],
            seed.BuyerParticipantId);
        await using var context = TicketQueryTestHelper.CreateContext(databaseName);
        var handler = TicketQueryTestHelper.CreateListMyTicketsHandler(context, identity);

        // Act
        var result = await handler.Handle(new ListMyTicketsQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Value.PersonalTickets[0].IsCheckedIn);
        Assert.NotNull(result.Value.PersonalTickets[0].CheckedInAt);
    }

    [Fact]
    public async Task ListMyTicketsQueryHandler_Should_SetRefundPending_When_EventCancelled()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = TicketQueryTestHelper.CreateVerifiedIdentity("auth0|list-refund", "refund@example.com");
        await TicketQueryTestHelper.SeedPaidOrderWithTicketsAsync(
            databaseName,
            identity,
            quantity: 1,
            cancelledEvent: true);
        await using var context = TicketQueryTestHelper.CreateContext(databaseName);
        var handler = TicketQueryTestHelper.CreateListMyTicketsHandler(context, identity);

        // Act
        var result = await handler.Handle(new ListMyTicketsQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Value.PersonalTickets[0].RefundPending);
    }
}
