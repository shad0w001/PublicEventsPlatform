using Domain.Events;
using Domain.Events.EventLocations;
using Domain.Events.Services;
using Domain.Tickets;
using Domain.Tickets.Services;
using Domain.Users;
using Domain.Users.Services;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureTests;

public class TicketModelTests
{
    [Fact]
    public void TicketTypeModel_Should_ExposeExpectedColumns_When_ApplicationDbContextModelIsBuilt()
    {
        // Arrange
        using var context = CreateContext();

        // Act
        var entityType = context.Model.FindEntityType(typeof(TicketType));

        // Assert
        Assert.NotNull(entityType);
        Assert.NotNull(entityType.FindProperty(nameof(TicketType.PriceCents)));
        Assert.NotNull(entityType.FindProperty(nameof(TicketType.Capacity)));
        Assert.NotNull(entityType.FindProperty(nameof(TicketType.SoldQuantity)));
        Assert.NotNull(entityType.FindProperty(nameof(TicketType.ReservedQuantity)));
    }

    [Fact]
    public void TicketModel_Should_NotContainShadowEventId1_When_ApplicationDbContextModelIsBuilt()
    {
        // Arrange
        using var context = CreateContext();

        // Act
        var ticketEntity = context.Model.FindEntityType(typeof(Ticket));
        var orderEntity = context.Model.FindEntityType(typeof(Order));

        // Assert
        Assert.NotNull(ticketEntity);
        Assert.NotNull(orderEntity);
        Assert.Null(ticketEntity.FindProperty("EventId1"));
        Assert.Null(orderEntity.FindProperty("EventId1"));
    }

    [Fact]
    public void TicketCodeModel_Should_UseTicketIdAsPrimaryKey_When_ApplicationDbContextModelIsBuilt()
    {
        // Arrange
        using var context = CreateContext();

        // Act
        var entityType = context.Model.FindEntityType(typeof(TicketCode));
        var primaryKey = entityType?.FindPrimaryKey();

        // Assert
        Assert.NotNull(entityType);
        Assert.NotNull(primaryKey);
        Assert.Single(primaryKey.Properties);
        Assert.Equal(nameof(TicketCode.TicketId), primaryKey.Properties[0].Name);
    }

    [Fact]
    public async Task TicketGraph_Should_PersistRoundTrip_When_SavedViaApplicationDbContext()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        Guid ticketTypeId;
        Guid ticketId;
        string manualCode;

        await using (var context = CreateContext(databaseName))
        {
            var buyer = UserService.ProvisionFromExternalIdentity(
                externalSubjectId: "auth0|ticket-buyer",
                email: "buyer@example.com",
                emailVerified: true,
                profilePictureUrl: null,
                defaultAvatarUrl: "/images/default-avatar.png",
                serviceRole: ServiceRole.User);
            context.Users.Add(buyer);

            var createResult = EventService.Create(EventTier.Small, "Paid Persist Event", buyer.Id, "/images/default-event-banner.png");
            var @event = createResult.Value.Event;
            var organizer = createResult.Value.Organizer;

            EventService.Update(
                @event,
                new EventUpdatePatch
                {
                    Description = "Paid event",
                    CategoryId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                    StartTime = new DateTime(2026, 8, 1, 18, 0, 0, DateTimeKind.Utc),
                    EndTime = new DateTime(2026, 8, 1, 22, 0, 0, DateTimeKind.Utc),
                    TimeZoneId = "Europe/Sofia",
                    AdmissionType = AdmissionType.Paid,
                    Locations =
                    [
                        new EventLocation
                        {
                            Name = "Hall",
                            Kind = EventLocationKind.Physical,
                            Address = "123 Main St",
                            City = "Sofia"
                        }
                    ]
                },
                actingUserId: buyer.Id);

            var ticketType = TicketTypeService.Create(
                @event,
                name: "General",
                description: "Entry",
                priceCents: 1500,
                capacity: 20).Value;

            EventService.Publish(@event, categoryExists: true, recentPublishCount: 0, maxPublishesPerWeek: 6);

            var order = OrderService.CreatePending(
                @event,
                ticketType,
                buyer.Id,
                quantity: 1,
                expiresAt: DateTime.UtcNow.AddMinutes(30)).Value;

            OrderService.ReserveInventory(ticketType, quantity: 1);
            OrderService.MarkPaid(order, ticketType, quantity: 1);

            var tickets = TicketService.IssueTickets(order, ticketType, buyer.Id, quantity: 1).Value;
            var ticket = tickets[0];
            manualCode = ticket.TicketCode!.ManualCode;

            context.Events.Add(@event);
            context.EventOrganizers.Add(organizer);
            context.TicketTypes.Add(ticketType);
            context.Orders.Add(order);
            context.Tickets.Add(ticket);
            context.TicketCodes.Add(ticket.TicketCode);
            await context.SaveChangesAsync();

            ticketTypeId = ticketType.Id;
            ticketId = ticket.Id;
        }

        // Act
        Ticket? loadedTicket;
        await using (var context = CreateContext(databaseName))
        {
            loadedTicket = await context.Tickets
                .Include(t => t.TicketCode)
                .Include(t => t.TicketType)
                .SingleAsync(t => t.Id == ticketId);
        }

        // Assert
        Assert.NotNull(loadedTicket);
        Assert.Equal(ticketTypeId, loadedTicket.TicketTypeId);
        Assert.NotNull(loadedTicket.TicketCode);
        Assert.Equal(manualCode, loadedTicket.TicketCode!.ManualCode);
        Assert.Equal(8, loadedTicket.TicketCode.ManualCode.Length);
        Assert.Equal(1500, loadedTicket.TicketType.PriceCents);
        Assert.Equal(1, loadedTicket.TicketType.SoldQuantity);
    }

    private static ApplicationDbContext CreateContext(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }
}
