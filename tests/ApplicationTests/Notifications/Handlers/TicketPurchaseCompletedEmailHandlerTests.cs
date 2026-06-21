using Application.Notifications.Handlers;
using Application.Notifications.Services;
using Domain.Events;
using Domain.Events.EventLocations;
using Domain.Events.Services;
using Domain.Tickets;
using Domain.Tickets.Events;
using Domain.Tickets.Services;
using Domain.Users;
using Domain.Users.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace ApplicationTests.Notifications.Handlers;

using ApplicationTests.Events;

public class TicketPurchaseCompletedEmailHandlerTests
{
    [Fact]
    public async Task HandleAsync_Should_SendPurchaseEmailWithTicketLinks_When_BuyerIsUser()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var buyer = CreateUser("auth0|buyer", "buyer@example.com");
        var (orderId, eventId, eventTitle, ticketIds) = await SeedPaidOrderAsync(
            databaseName,
            buyer.Id,
            quantity: 2,
            userToSeed: buyer);
        var fakeEmailSender = new FakeEmailSender();
        var handler = CreateHandler(databaseName, fakeEmailSender);

        // Act
        await handler.HandleAsync(
            new TicketPurchaseCompleted(orderId, eventId, buyer.Id, 2),
            CancellationToken.None);

        // Assert
        Assert.NotNull(fakeEmailSender.LastMessage);
        Assert.Equal(["buyer@example.com"], fakeEmailSender.LastMessage!.To);
        Assert.Contains(eventTitle, fakeEmailSender.LastMessage.Subject);
        Assert.Contains($"http://localhost:5173/orders/{orderId}", fakeEmailSender.LastMessage.Body);
        Assert.Contains($"http://localhost:5173/tickets/{ticketIds[0]}", fakeEmailSender.LastMessage.Body);
        Assert.Contains($"http://localhost:5173/tickets/{ticketIds[1]}", fakeEmailSender.LastMessage.Body);
    }

    [Fact]
    public async Task HandleAsync_Should_SendToOrganizerPlusEmails_When_BuyerIsGroup()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|group-owner", "group-owner@example.com");
        var organizer = CreateUser("auth0|group-organizer", "group-organizer@example.com");
        var group = SeedGroup(databaseName, owner, [(organizer, Domain.Groups.GroupMemberRole.Organizer)]);
        var (orderId, eventId, _, _) = await SeedPaidOrderAsync(
            databaseName,
            group.Id,
            quantity: 1);
        var fakeEmailSender = new FakeEmailSender();
        var handler = CreateHandler(databaseName, fakeEmailSender);

        // Act
        await handler.HandleAsync(
            new TicketPurchaseCompleted(orderId, eventId, group.Id, 1),
            CancellationToken.None);

        // Assert
        Assert.NotNull(fakeEmailSender.LastMessage);
        Assert.Equal(2, fakeEmailSender.LastMessage!.To.Count);
        Assert.Contains("group-owner@example.com", fakeEmailSender.LastMessage.To);
        Assert.Contains("group-organizer@example.com", fakeEmailSender.LastMessage.To);
    }

    private static TicketPurchaseCompletedEmailHandler CreateHandler(
        string databaseName,
        FakeEmailSender fakeEmailSender)
    {
        var context = NotificationHandlerTestSupport.CreateContext(databaseName);
        return new TicketPurchaseCompletedEmailHandler(
            context,
            new NotificationRecipientService(context),
            NotificationHandlerTestSupport.CreateLinkBuilder(),
            fakeEmailSender,
            NullLogger<TicketPurchaseCompletedEmailHandler>.Instance);
    }

    private static async Task<(Guid OrderId, Guid EventId, string EventTitle, IReadOnlyList<Guid> TicketIds)> SeedPaidOrderAsync(
        string databaseName,
        Guid buyerParticipantId,
        int quantity,
        User? userToSeed = null)
    {
        var createResult = EventService.Create(EventTier.Small, "Paid Concert", buyerParticipantId, EventTestConstants.DefaultBannerUrl);
        var @event = createResult.Value.Event;
        @event.Description = "Description";
        @event.CategoryId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        @event.StartTime = new DateTime(2026, 8, 1, 18, 0, 0, DateTimeKind.Utc);
        @event.EndTime = new DateTime(2026, 8, 1, 22, 0, 0, DateTimeKind.Utc);
        @event.TimeZoneId = "Europe/Sofia";
        @event.AdmissionType = AdmissionType.Paid;
        @event.Locations.Add(new EventLocation
        {
            Name = "Hall",
            Kind = EventLocationKind.Physical,
            Address = "123 Main St",
            City = "Sofia"
        });

        var ticketType = TicketTypeService.Create(@event, "General", null, 2500, 100).Value;
        @event.TicketTypes.Add(ticketType);
        EventService.Publish(@event, categoryExists: true, recentPublishCount: 0, maxPublishesPerWeek: 6);

        var order = OrderService.CreatePending(
            @event,
            ticketType,
            buyerParticipantId,
            quantity,
            DateTime.UtcNow.AddMinutes(30)).Value;
        OrderService.ReserveInventory(ticketType, quantity);
        OrderService.MarkPaid(order, ticketType, quantity);
        var tickets = TicketService.IssueTickets(order, ticketType, buyerParticipantId, quantity).Value;

        await using var context = NotificationHandlerTestSupport.CreateContext(databaseName);
        if (userToSeed is not null)
        {
            context.Users.Add(userToSeed);
        }

        context.Events.Add(@event);
        context.EventOrganizers.Add(createResult.Value.Organizer);
        context.TicketTypes.Add(ticketType);
        context.Orders.Add(order);
        context.Tickets.AddRange(tickets);
        await context.SaveChangesAsync();

        return (order.Id, @event.Id, @event.Title, tickets.Select(t => t.Id).ToList());
    }

    private static Domain.Groups.Group SeedGroup(
        string databaseName,
        User owner,
        IReadOnlyList<(User User, Domain.Groups.GroupMemberRole Role)> additionalMembers)
    {
        var createResult = Domain.Groups.Services.GroupService.Create(
            "Ticket Org",
            "",
            Domain.Groups.GroupJoinPolicy.Open,
            owner.Id,
            "/images/default-group.png");
        var group = createResult.Value.Group;

        using var context = NotificationHandlerTestSupport.CreateContext(databaseName);
        context.Users.Add(owner);
        context.Groups.Add(group);
        context.GroupMemberships.Add(createResult.Value.OwnerMembership);

        foreach (var (user, role) in additionalMembers)
        {
            if (user.Id == owner.Id)
            {
                continue;
            }

            context.Users.Add(user);
            context.GroupMemberships.Add(Domain.Groups.GroupMembership.Create(group.Id, user.Id, role));
        }

        context.SaveChanges();
        return group;
    }

    private static User CreateUser(string subject, string email) =>
        UserService.ProvisionFromExternalIdentity(
            subject,
            email,
            emailVerified: true,
            profilePictureUrl: null,
            NotificationHandlerTestSupport.DefaultAvatarUrl,
            ServiceRole.User);
}
