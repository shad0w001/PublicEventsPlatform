using Application.Abstractions.Data;
using Application.Abstractions.Payments;
using Application.Tickets.ProcessStripeWebhook;
using Domain.Events;
using Domain.Events.EventLocations;
using Domain.Events.Services;
using Domain.Tickets;
using Domain.Tickets.Services;
using Domain.Users;
using Domain.Users.Services;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SharedKernel;

namespace ApplicationTests.Tickets;

using ApplicationTests.Events;

public class ProcessStripeWebhookCommandHandlerTests
{
    private const string CheckoutSessionId = "cs_test_webhook_session";

    [Fact]
    public async Task ProcessStripeWebhookCommandHandler_Should_FulfillOrder_When_CheckoutSessionCompleted()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var (orderId, eventId, ticketTypeId, buyerId) = await SeedPendingOrderAsync(databaseName);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, CompletedEvent(orderId, eventId, ticketTypeId));

        // Act
        var result = await handler.Handle(
            new ProcessStripeWebhookCommand("{}", "sig"),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);

        var order = await context.Orders
            .Include(o => o.Tickets)
            .SingleAsync(o => o.Id == orderId);
        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.Equal(2, order.Tickets.Count);
        Assert.Equal("pi_test_webhook", order.PaymentIntentId);

        var ticketType = await context.TicketTypes.SingleAsync(t => t.Id == ticketTypeId);
        Assert.Equal(2, ticketType.SoldQuantity);
        Assert.Equal(0, ticketType.ReservedQuantity);

        var attendee = await context.EventAttendees.SingleAsync(
            a => a.EventId == eventId && a.ParticipantId == buyerId);
        Assert.Equal(2, attendee.TicketCount);
        Assert.Null(attendee.Status);

        Assert.Equal(2, await context.TicketCodes.CountAsync());
    }

    [Fact]
    public async Task ProcessStripeWebhookCommandHandler_Should_BeIdempotent_When_CheckoutSessionCompletedReplayed()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var (orderId, eventId, ticketTypeId, _) = await SeedPendingOrderAsync(databaseName);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, CompletedEvent(orderId, eventId, ticketTypeId));

        // Act
        var first = await handler.Handle(new ProcessStripeWebhookCommand("{}", "sig"), CancellationToken.None);
        var second = await handler.Handle(new ProcessStripeWebhookCommand("{}", "sig"), CancellationToken.None);

        // Assert
        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(2, await context.Tickets.CountAsync(t => t.OrderId == orderId));
        Assert.Equal(2, await context.TicketCodes.CountAsync());
    }

    [Fact]
    public async Task ProcessStripeWebhookCommandHandler_Should_ReleaseReservation_When_CheckoutSessionExpired()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var (orderId, eventId, ticketTypeId, _) = await SeedPendingOrderAsync(databaseName);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, ExpiredEvent(orderId, eventId, ticketTypeId));

        // Act
        var result = await handler.Handle(
            new ProcessStripeWebhookCommand("{}", "sig"),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);

        var order = await context.Orders.SingleAsync(o => o.Id == orderId);
        Assert.Equal(OrderStatus.Expired, order.Status);

        var ticketType = await context.TicketTypes.SingleAsync(t => t.Id == ticketTypeId);
        Assert.Equal(0, ticketType.ReservedQuantity);
        Assert.Equal(0, ticketType.SoldQuantity);
    }

    [Fact]
    public async Task ProcessStripeWebhookCommandHandler_Should_BeIdempotent_When_CheckoutSessionExpiredReplayed()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var (orderId, eventId, ticketTypeId, _) = await SeedPendingOrderAsync(databaseName);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, ExpiredEvent(orderId, eventId, ticketTypeId));

        // Act
        var first = await handler.Handle(new ProcessStripeWebhookCommand("{}", "sig"), CancellationToken.None);
        var second = await handler.Handle(new ProcessStripeWebhookCommand("{}", "sig"), CancellationToken.None);

        // Assert
        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(OrderStatus.Expired, (await context.Orders.SingleAsync(o => o.Id == orderId)).Status);
    }

    [Fact]
    public async Task ProcessStripeWebhookCommandHandler_Should_AcknowledgeWithoutChanges_When_CompletedArrivesForExpiredOrder()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var (orderId, eventId, ticketTypeId, _) = await SeedPendingOrderAsync(databaseName);
        await using var context = CreateContext(databaseName);
        var order = await context.Orders.SingleAsync(o => o.Id == orderId);
        var ticketType = await context.TicketTypes.SingleAsync(t => t.Id == ticketTypeId);
        OrderService.MarkExpired(order, ticketType, order.Quantity);
        await context.SaveChangesAsync(CancellationToken.None);

        var handler = CreateHandler(context, CompletedEvent(orderId, eventId, ticketTypeId));

        // Act
        var result = await handler.Handle(
            new ProcessStripeWebhookCommand("{}", "sig"),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Expired, (await context.Orders.SingleAsync(o => o.Id == orderId)).Status);
        Assert.False(await context.Tickets.AnyAsync());
    }

    [Fact]
    public async Task ProcessStripeWebhookCommandHandler_Should_ReturnCheckoutSessionMismatch_When_SessionIdDiffers()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var (orderId, eventId, ticketTypeId, _) = await SeedPendingOrderAsync(databaseName);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(
            context,
            CompletedEvent(orderId, eventId, ticketTypeId, sessionId: "cs_other"));

        // Act
        var result = await handler.Handle(
            new ProcessStripeWebhookCommand("{}", "sig"),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(TicketErrors.CheckoutSessionMismatch.Code, result.Error.Code);
    }

    [Fact]
    public async Task ProcessStripeWebhookCommandHandler_Should_ReturnOrderNotFound_When_OrderDoesNotExist()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        await using var context = CreateContext(databaseName);
        var missingOrderId = Guid.NewGuid();
        var handler = CreateHandler(
            context,
            CompletedEvent(missingOrderId, Guid.NewGuid(), Guid.NewGuid()));

        // Act
        var result = await handler.Handle(
            new ProcessStripeWebhookCommand("{}", "sig"),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(TicketErrors.OrderNotFound(missingOrderId).Code, result.Error.Code);
    }

    [Fact]
    public async Task ProcessStripeWebhookCommandHandler_Should_IgnoreUnsupportedEvents_When_EventTypeIsIgnored()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var (orderId, _, _, _) = await SeedPendingOrderAsync(databaseName);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(
            context,
            new StripeWebhookEvent(
                StripeWebhookEventType.Ignored,
                string.Empty,
                Guid.Empty,
                Guid.Empty,
                Guid.Empty,
                null));

        // Act
        var result = await handler.Handle(
            new ProcessStripeWebhookCommand("{}", "sig"),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Pending, (await context.Orders.SingleAsync(o => o.Id == orderId)).Status);
    }

    private static ProcessStripeWebhookCommandHandler CreateHandler(
        ApplicationDbContext context,
        StripeWebhookEvent webhookEvent) =>
        new(
            context,
            new FakeStripeWebhookVerifier(webhookEvent),
            new PassThroughTicketTypeRowLock(context),
            NullLogger<ProcessStripeWebhookCommandHandler>.Instance);

    private static StripeWebhookEvent CompletedEvent(
        Guid orderId,
        Guid eventId,
        Guid ticketTypeId,
        string sessionId = CheckoutSessionId) =>
        new(
            StripeWebhookEventType.CheckoutSessionCompleted,
            sessionId,
            orderId,
            eventId,
            ticketTypeId,
            "pi_test_webhook");

    private static StripeWebhookEvent ExpiredEvent(
        Guid orderId,
        Guid eventId,
        Guid ticketTypeId) =>
        new(
            StripeWebhookEventType.CheckoutSessionExpired,
            CheckoutSessionId,
            orderId,
            eventId,
            ticketTypeId,
            null);

    private static async Task<(Guid OrderId, Guid EventId, Guid TicketTypeId, Guid BuyerId)> SeedPendingOrderAsync(
        string databaseName)
    {
        await using var context = CreateContext(databaseName);
        var buyer = new User
        {
            ExternalSubjectId = "auth0|webhook-buyer",
            Email = "webhook-buyer@example.com",
            EmailVerified = true,
            Username = "webhook-buyer",
            Bio = string.Empty,
            ProfilePictureUrl = "/images/default-avatar.png"
        };

        var createResult = EventService.Create(EventTier.Small, "Paid Event", buyer.Id, EventTestConstants.DefaultBannerUrl);
        var @event = createResult.Value.Event;
        var organizer = createResult.Value.Organizer;
        var categoryId = SeedCategoryInContext(context);
        MakePublishReadyViaUpdate(@event, categoryId, buyer.Id, AdmissionType.Paid);
        var ticketType = TicketTypeService.Create(@event, "General", "Entry", 2500, 100).Value;
        EventService.Publish(@event, categoryExists: true, recentPublishCount: 0, maxPublishesPerWeek: 6);

        var orderResult = OrderService.CreatePending(
            @event,
            ticketType,
            buyer.Id,
            quantity: 2,
            DateTime.UtcNow.AddMinutes(30));
        var order = orderResult.Value;
        OrderService.ReserveInventory(ticketType, quantity: 2);
        OrderService.AttachCheckoutSession(order, CheckoutSessionId, null);

        context.Users.Add(buyer);
        context.Events.Add(@event);
        context.EventOrganizers.Add(organizer);
        context.Orders.Add(order);
        await context.SaveChangesAsync(CancellationToken.None);

        return (order.Id, @event.Id, ticketType.Id, buyer.Id);
    }

    private static Guid SeedCategoryInContext(ApplicationDbContext context)
    {
        var existing = context.EventCategories.FirstOrDefault();
        if (existing is not null)
        {
            return existing.Id;
        }

        var category = new EventCategory { Name = "Music" };
        context.EventCategories.Add(category);
        return category.Id;
    }

    private static void MakePublishReadyViaUpdate(
        Event @event,
        Guid categoryId,
        Guid actingUserId,
        AdmissionType admissionType)
    {
        var start = new DateTime(2026, 7, 1, 18, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 7, 1, 22, 0, 0, DateTimeKind.Utc);
        var patch = new EventUpdatePatch
        {
            Description = "A test event description",
            CategoryId = categoryId,
            StartTime = start,
            EndTime = end,
            TimeZoneId = "Europe/Sofia",
            AdmissionType = admissionType,
            Locations =
            [
                new EventLocation
                {
                    Name = "Main Hall",
                    Kind = EventLocationKind.Physical,
                    Address = "123 Main St",
                    City = "Sofia"
                }
            ]
        };

        var updateResult = EventService.Update(@event, patch, actingUserId);
        if (updateResult.IsFailure)
        {
            throw new InvalidOperationException(updateResult.Error.Code);
        }
    }

    private static ApplicationDbContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new ApplicationDbContext(options);
    }

    private sealed class FakeStripeWebhookVerifier(StripeWebhookEvent webhookEvent) : IStripeWebhookVerifier
    {
        public Task<Result<StripeWebhookEvent>> VerifyAsync(
            string json,
            string signatureHeader,
            CancellationToken cancellationToken) =>
            Task.FromResult<Result<StripeWebhookEvent>>(webhookEvent);
    }

    private sealed class PassThroughTicketTypeRowLock(ApplicationDbContext context) : ITicketTypeRowLock
    {
        public async Task<Result<T>> ExecuteAsync<T>(
            Guid ticketTypeId,
            Func<TicketType, CancellationToken, Task<Result<T>>> work,
            CancellationToken cancellationToken)
        {
            var ticketType = await context.TicketTypes
                .Include(t => t.Event)
                .FirstOrDefaultAsync(t => t.Id == ticketTypeId, cancellationToken);

            if (ticketType is null)
            {
                return Result.Failure<T>(TicketErrors.TicketTypeNotFound(ticketTypeId));
            }

            var workResult = await work(ticketType, cancellationToken);

            if (workResult.IsFailure)
            {
                context.ChangeTracker.Clear();
                return workResult;
            }

            await context.SaveChangesAsync(cancellationToken);
            return workResult;
        }
    }
}
