using Application.Notifications.Handlers;
using Application.Notifications.Services;
using Domain.Events;
using Domain.Events.EventLocations;
using Domain.Events.Events;
using Domain.Events.Services;
using Domain.Tickets;
using Domain.Tickets.Services;
using Domain.Users;
using Domain.Users.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace ApplicationTests.Notifications.Handlers;

using ApplicationTests.Events;

public class EventCancelledEmailHandlerTests
{
    [Fact]
    public async Task HandleAsync_Should_SendRefundForthcomingEmailWithOrderLink_When_PaidPersonalOrderExists()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var buyer = CreateUser("auth0|cancel-buyer", "buyer@example.com");
        var (eventId, orderId) = await SeedCancelledPaidEventWithOrderAsync(databaseName, buyer.Id, buyer);
        var fakeEmailSender = new FakeEmailSender();
        var handler = CreateHandler(databaseName, fakeEmailSender);

        // Act
        await handler.HandleAsync(new EventCancelled(eventId), CancellationToken.None);

        // Assert
        Assert.Single(fakeEmailSender.Messages);
        Assert.Equal(["buyer@example.com"], fakeEmailSender.Messages[0].To);
        Assert.Contains("Event cancelled", fakeEmailSender.Messages[0].Subject);
        Assert.Contains("refund will be processed", fakeEmailSender.Messages[0].Body);
        Assert.Contains($"http://localhost:5173/orders/{orderId}", fakeEmailSender.Messages[0].Body);
    }

    [Fact]
    public async Task HandleAsync_Should_SendToOrganizerPlusEmails_When_PaidGroupOrderExists()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|cancel-owner", "cancel-owner@example.com");
        var organizer = CreateUser("auth0|cancel-organizer", "cancel-organizer@example.com");
        var group = SeedGroup(databaseName, owner, [(organizer, Domain.Groups.GroupMemberRole.Organizer)]);
        var (eventId, _) = await SeedCancelledPaidEventWithOrderAsync(databaseName, group.Id);
        var fakeEmailSender = new FakeEmailSender();
        var handler = CreateHandler(databaseName, fakeEmailSender);

        // Act
        await handler.HandleAsync(new EventCancelled(eventId), CancellationToken.None);

        // Assert
        Assert.Single(fakeEmailSender.Messages);
        Assert.Equal(2, fakeEmailSender.Messages[0].To.Count);
        Assert.Contains("cancel-owner@example.com", fakeEmailSender.Messages[0].To);
        Assert.Contains("cancel-organizer@example.com", fakeEmailSender.Messages[0].To);
        Assert.Contains("refund will be processed", fakeEmailSender.Messages[0].Body);
    }

    [Fact]
    public async Task HandleAsync_Should_EmailGoingAttendeesOnly_When_FreeEventHasMixedRsvps()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var host = CreateUser("auth0|cancel-host", "host@example.com");
        var goingUser = CreateUser("auth0|cancel-going", "going@example.com");
        var interestedUser = CreateUser("auth0|cancel-interested", "interested@example.com");
        var eventId = await SeedCancelledFreeEventWithAttendeesAsync(
            databaseName,
            host,
            [
                (goingUser.Id, EventAttendeeStatus.Going),
                (interestedUser.Id, EventAttendeeStatus.Interested)
            ],
            [goingUser, interestedUser]);
        var fakeEmailSender = new FakeEmailSender();
        var handler = CreateHandler(databaseName, fakeEmailSender);

        // Act
        await handler.HandleAsync(new EventCancelled(eventId), CancellationToken.None);

        // Assert
        Assert.Equal(2, fakeEmailSender.Messages.Count);
        Assert.Contains(fakeEmailSender.Messages, m => m.To.SequenceEqual(["going@example.com"]));
        Assert.Contains(fakeEmailSender.Messages, m => m.To.SequenceEqual(["host@example.com"]));
        Assert.DoesNotContain(fakeEmailSender.Messages, m => m.To.Contains("interested@example.com"));
        Assert.All(fakeEmailSender.Messages, m =>
        {
            Assert.Contains("has been cancelled", m.Body);
            Assert.Contains($"http://localhost:5173/events/{eventId}", m.Body);
            Assert.DoesNotContain(m.Body, "refund will be processed");
        });
    }

    [Fact]
    public async Task HandleAsync_Should_SendToOrganizerPlusEmails_When_FreeGroupIsGoing()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|free-group-owner", "free-group-owner@example.com");
        var organizer = CreateUser("auth0|free-group-organizer", "free-group-organizer@example.com");
        var group = SeedGroup(databaseName, owner, [(organizer, Domain.Groups.GroupMemberRole.Organizer)]);
        var host = CreateUser("auth0|free-group-host", "free-group-host@example.com");
        var eventId = await SeedCancelledFreeEventWithAttendeesAsync(
            databaseName,
            host,
            [(group.Id, EventAttendeeStatus.Going)]);
        var fakeEmailSender = new FakeEmailSender();
        var handler = CreateHandler(databaseName, fakeEmailSender);

        // Act
        await handler.HandleAsync(new EventCancelled(eventId), CancellationToken.None);

        // Assert
        Assert.Equal(2, fakeEmailSender.Messages.Count);
        var groupMessage = Assert.Single(
            fakeEmailSender.Messages,
            m => m.To.Contains("free-group-owner@example.com"));
        Assert.Contains("free-group-organizer@example.com", groupMessage.To);
        Assert.Contains(
            fakeEmailSender.Messages,
            m => m.To.SequenceEqual(["free-group-host@example.com"]));
    }

    [Fact]
    public async Task HandleAsync_Should_NotSendEmail_When_PaidOrderHasNoRecipients()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var buyer = CreateUser("auth0|cancel-buyer", "buyer@example.com");
        var (eventId, _) = await SeedCancelledPaidEventWithOrderAsync(
            databaseName,
            Guid.NewGuid(),
            buyer);
        var fakeEmailSender = new FakeEmailSender();
        var handler = CreateHandler(databaseName, fakeEmailSender);

        // Act
        await handler.HandleAsync(new EventCancelled(eventId), CancellationToken.None);

        // Assert
        Assert.Empty(fakeEmailSender.Messages);
    }

    private static EventCancelledEmailHandler CreateHandler(
        string databaseName,
        FakeEmailSender fakeEmailSender)
    {
        var context = NotificationHandlerTestSupport.CreateContext(databaseName);
        return new EventCancelledEmailHandler(
            context,
            new NotificationRecipientService(context),
            NotificationHandlerTestSupport.CreateLinkBuilder(),
            fakeEmailSender,
            NullLogger<EventCancelledEmailHandler>.Instance);
    }

    private static async Task<(Guid EventId, Guid OrderId)> SeedCancelledPaidEventWithOrderAsync(
        string databaseName,
        Guid buyerParticipantId,
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
            1,
            DateTime.UtcNow.AddMinutes(30)).Value;
        OrderService.ReserveInventory(ticketType, 1);
        OrderService.MarkPaid(order, ticketType, 1);
        EventService.Cancel(@event);

        await using var context = NotificationHandlerTestSupport.CreateContext(databaseName);
        if (userToSeed is not null)
        {
            context.Users.Add(userToSeed);
        }

        context.Events.Add(@event);
        context.EventOrganizers.Add(createResult.Value.Organizer);
        context.TicketTypes.Add(ticketType);
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        return (@event.Id, order.Id);
    }

    private static async Task<Guid> SeedCancelledFreeEventWithAttendeesAsync(
        string databaseName,
        User hostUser,
        IReadOnlyList<(Guid ParticipantId, EventAttendeeStatus Status)> attendees,
        IReadOnlyList<User>? additionalUsers = null)
    {
        var createResult = EventService.Create(EventTier.Small, "Community Meetup", hostUser.Id, EventTestConstants.DefaultBannerUrl);
        var @event = createResult.Value.Event;
        @event.Description = "Description";
        @event.CategoryId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        @event.StartTime = new DateTime(2026, 8, 1, 18, 0, 0, DateTimeKind.Utc);
        @event.EndTime = new DateTime(2026, 8, 1, 22, 0, 0, DateTimeKind.Utc);
        @event.TimeZoneId = "Europe/Sofia";
        @event.AdmissionType = AdmissionType.Free;
        @event.Locations.Add(new EventLocation
        {
            Name = "Hall",
            Kind = EventLocationKind.Physical,
            Address = "123 Main St",
            City = "Sofia"
        });
        EventService.Publish(@event, categoryExists: true, recentPublishCount: 0, maxPublishesPerWeek: 6);
        EventAttendeeService.EnsureHostGoing(@event, DateTime.UtcNow);
        EventService.Cancel(@event);

        await using var context = NotificationHandlerTestSupport.CreateContext(databaseName);
        context.Users.Add(hostUser);
        if (additionalUsers is not null)
        {
            context.Users.AddRange(additionalUsers);
        }

        context.Events.Add(@event);
        context.EventOrganizers.Add(createResult.Value.Organizer);

        foreach (var (participantId, status) in attendees)
        {
            var attendee = EventAttendee.Create(@event.Id, participantId);
            attendee.Status = status;
            attendee.RegisteredAt = DateTime.UtcNow;
            context.EventAttendees.Add(attendee);
        }

        await context.SaveChangesAsync();
        return @event.Id;
    }

    private static Domain.Groups.Group SeedGroup(
        string databaseName,
        User owner,
        IReadOnlyList<(User User, Domain.Groups.GroupMemberRole Role)> additionalMembers)
    {
        var createResult = Domain.Groups.Services.GroupService.Create(
            "Cancel Org",
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
