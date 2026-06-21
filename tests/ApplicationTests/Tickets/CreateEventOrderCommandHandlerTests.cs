using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Payments;
using Application.Events.Services;
using Application.Payments;
using Application.Tickets.CreateEventOrder;
using Application.Users;
using Application.Users.Services;
using Domain.Events;
using Domain.Events.EventLocations;
using Domain.Events.Services;
using Domain.Groups;
using Domain.Groups.Services;
using Domain.Tickets;
using Domain.Tickets.Services;
using Domain.Users;
using Domain.Users.Services;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace ApplicationTests.Tickets;

using ApplicationTests.Events;

public class CreateEventOrderCommandHandlerTests
{
    private const string DefaultAvatarUrl = "/images/default-avatar.png";
    private const string DefaultGroupImageUrl = "/images/default-group.png";

    [Fact]
    public async Task CreateEventOrderCommandHandler_Should_ReturnCheckoutUrl_When_PublishedPaidEvent()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|order-buyer", "buyer@example.com");
        var (eventId, ticketTypeId) = await SeedPublishedPaidEventWithTicketTypeAsync(databaseName, identity);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity, new FakeCheckoutSessionProvider());

        // Act
        var result = await handler.Handle(
            new CreateEventOrderCommand(eventId, ticketTypeId, Quantity: 2),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value.OrderId);
        Assert.Equal("https://checkout.stripe.test/session", result.Value.CheckoutUrl);
        Assert.True(result.Value.ExpiresAt > DateTime.UtcNow);

        var order = await context.Orders.SingleAsync(o => o.Id == result.Value.OrderId);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal("cs_test_session", order.CheckoutSessionId);
        Assert.Equal(2, order.Quantity);

        var ticketType = await context.TicketTypes.SingleAsync(t => t.Id == ticketTypeId);
        Assert.Equal(2, ticketType.ReservedQuantity);
    }

    [Fact]
    public async Task CreateEventOrderCommandHandler_Should_ReturnInvalidQuantity_When_QuantityExceedsRemaining()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|order-qty", "qty@example.com");
        var (eventId, ticketTypeId) = await SeedPublishedPaidEventWithTicketTypeAsync(
            databaseName,
            identity,
            capacity: 3);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity, new FakeCheckoutSessionProvider());

        // Act
        var result = await handler.Handle(
            new CreateEventOrderCommand(eventId, ticketTypeId, Quantity: 5),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(TicketErrors.InvalidQuantity.Code, result.Error.Code);
        Assert.False(await context.Orders.AnyAsync());
    }

    [Fact]
    public async Task CreateEventOrderCommandHandler_Should_ReturnEventCancelled_When_EventIsCancelled()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|order-cancel", "cancel@example.com");
        var (eventId, ticketTypeId) = await SeedPublishedPaidEventWithTicketTypeAsync(
            databaseName,
            identity,
            cancelled: true);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity, new FakeCheckoutSessionProvider());

        // Act
        var result = await handler.Handle(
            new CreateEventOrderCommand(eventId, ticketTypeId, Quantity: 1),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(TicketErrors.EventCancelled.Code, result.Error.Code);
    }

    [Fact]
    public async Task CreateEventOrderCommandHandler_Should_ReturnPaidAdmissionRequired_When_EventIsFree()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|order-free", "free@example.com");
        var (eventId, ticketTypeId) = await SeedPublishedPaidEventWithTicketTypeAsync(
            databaseName,
            identity,
            admissionType: AdmissionType.Free);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity, new FakeCheckoutSessionProvider());

        // Act
        var result = await handler.Handle(
            new CreateEventOrderCommand(eventId, ticketTypeId, Quantity: 1),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(TicketErrors.PaidAdmissionRequired.Code, result.Error.Code);
    }

    [Fact]
    public async Task CreateEventOrderCommandHandler_Should_RollBackReservation_When_CheckoutCreationFails()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|order-stripe-fail", "stripe-fail@example.com");
        var (eventId, ticketTypeId) = await SeedPublishedPaidEventWithTicketTypeAsync(databaseName, identity);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(
            context,
            identity,
            new FakeCheckoutSessionProvider { ShouldFail = true });

        // Act
        var result = await handler.Handle(
            new CreateEventOrderCommand(eventId, ticketTypeId, Quantity: 2),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(PaymentErrors.CheckoutSessionCreationFailed.Code, result.Error.Code);
        Assert.False(await context.Orders.AnyAsync());

        await using var verifyContext = CreateContext(databaseName);
        var ticketType = await verifyContext.TicketTypes.SingleAsync(t => t.Id == ticketTypeId);
        Assert.Equal(0, ticketType.ReservedQuantity);
    }

    [Fact]
    public async Task CreateEventOrderCommandHandler_Should_ReturnInsufficientPurchasePermissions_When_BuyingForGroupWithoutRole()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var ownerIdentity = CreateVerifiedIdentity("auth0|order-group-owner", "owner@example.com");
        var memberIdentity = CreateVerifiedIdentity("auth0|order-group-member", "member@example.com");
        var (eventId, ticketTypeId, groupId) = await SeedPublishedPaidEventWithGroupAsync(
            databaseName,
            ownerIdentity,
            memberIdentity);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, memberIdentity, new FakeCheckoutSessionProvider());

        // Act
        var result = await handler.Handle(
            new CreateEventOrderCommand(eventId, ticketTypeId, Quantity: 1, ParticipantId: groupId),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(TicketErrors.InsufficientPurchasePermissions.Code, result.Error.Code);
    }

    private static CreateEventOrderCommandHandler CreateHandler(
        ApplicationDbContext context,
        IUserIdentityAccessor identity,
        ICheckoutSessionProvider checkoutSessionProvider) =>
        new(
            context,
            new CurrentUserService(
                context,
                identity,
                Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl })),
            identity,
            new EventAccessService(context),
            new PassThroughTicketTypeRowLock(context),
            checkoutSessionProvider,
            Options.Create(new StripeOptions
            {
                SecretKey = "sk_test_fake",
                SuccessUrlBase = "http://localhost:5173/orders",
                CancelUrlBase = "http://localhost:5173/events",
                PendingOrderTtlMinutes = 30
            }));

    private static async Task<(Guid EventId, Guid TicketTypeId)> SeedPublishedPaidEventWithTicketTypeAsync(
        string databaseName,
        FakeUserIdentityAccessor identity,
        AdmissionType admissionType = AdmissionType.Paid,
        int capacity = 100,
        bool cancelled = false)
    {
        await using var context = CreateContext(databaseName);
        var currentUserService = new CurrentUserService(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));
        var user = (await currentUserService.GetOrProvisionAsync(CancellationToken.None)).Value;

        var createResult = EventService.Create(EventTier.Small, "Paid Event", user.Id, EventTestConstants.DefaultBannerUrl);
        var @event = createResult.Value.Event;
        var organizer = createResult.Value.Organizer;
        var categoryId = SeedCategoryInContext(context);
        MakePublishReadyViaUpdate(@event, categoryId, user.Id, admissionType);

        TicketType ticketType;
        if (admissionType == AdmissionType.Paid)
        {
            ticketType = TicketTypeService.Create(@event, "General", "Entry", 2500, capacity).Value;
        }
        else
        {
            ticketType = new TicketType
            {
                EventId = @event.Id,
                Event = @event,
                Name = "Stale Type",
                Description = string.Empty,
                PriceCents = 2500,
                Capacity = capacity
            };
            @event.TicketTypes.Add(ticketType);
        }

        EventService.Publish(@event, categoryExists: true, recentPublishCount: 0, maxPublishesPerWeek: 6);

        if (cancelled)
        {
            EventService.Cancel(@event);
        }

        context.Events.Add(@event);
        context.EventOrganizers.Add(organizer);
        await context.SaveChangesAsync(CancellationToken.None);
        return (@event.Id, ticketType.Id);
    }

    private static async Task<(Guid EventId, Guid TicketTypeId, Guid GroupId)> SeedPublishedPaidEventWithGroupAsync(
        string databaseName,
        FakeUserIdentityAccessor ownerIdentity,
        FakeUserIdentityAccessor memberIdentity)
    {
        await using var context = CreateContext(databaseName);
        var ownerUserService = new CurrentUserService(
            context,
            ownerIdentity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));
        var owner = (await ownerUserService.GetOrProvisionAsync(CancellationToken.None)).Value;

        var memberUserService = new CurrentUserService(
            context,
            memberIdentity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));
        var member = (await memberUserService.GetOrProvisionAsync(CancellationToken.None)).Value;

        var groupCreateResult = GroupService.Create(
            "Test Org",
            "",
            GroupJoinPolicy.Open,
            owner.Id,
            DefaultGroupImageUrl);
        var group = groupCreateResult.Value.Group;
        var memberMembership = GroupMembership.Create(group.Id, member.Id, GroupMemberRole.Member);

        var createResult = EventService.Create(EventTier.Small, "Paid Event", owner.Id, EventTestConstants.DefaultBannerUrl);
        var @event = createResult.Value.Event;
        var organizer = createResult.Value.Organizer;
        var categoryId = SeedCategoryInContext(context);
        MakePublishReadyViaUpdate(@event, categoryId, owner.Id, AdmissionType.Paid);
        var ticketType = TicketTypeService.Create(@event, "General", "Entry", 2500, 100).Value;
        EventService.Publish(@event, categoryExists: true, recentPublishCount: 0, maxPublishesPerWeek: 6);

        context.Groups.Add(group);
        context.GroupMemberships.Add(groupCreateResult.Value.OwnerMembership);
        context.GroupMemberships.Add(memberMembership);
        context.Events.Add(@event);
        context.EventOrganizers.Add(organizer);
        await context.SaveChangesAsync(CancellationToken.None);

        return (@event.Id, ticketType.Id, group.Id);
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

    private static FakeUserIdentityAccessor CreateVerifiedIdentity(string externalSubjectId, string email) =>
        new()
        {
            IsAuthenticated = true,
            ExternalSubjectId = externalSubjectId,
            Email = email,
            EmailVerified = true,
            ServiceRole = ServiceRole.User
        };

    private static ApplicationDbContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new ApplicationDbContext(options);
    }

    private sealed class FakeUserIdentityAccessor : IUserIdentityAccessor
    {
        public bool IsAuthenticated { get; init; }
        public string ExternalSubjectId { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public bool EmailVerified { get; init; }
        public ServiceRole ServiceRole { get; init; }
        public string? ProfilePictureUrl { get; init; }
    }

    private sealed class FakeCheckoutSessionProvider : ICheckoutSessionProvider
    {
        public bool ShouldFail { get; init; }

        public Task<Result<CheckoutSessionResult>> CreateAsync(
            CheckoutSessionRequest request,
            CancellationToken cancellationToken)
        {
            if (ShouldFail)
            {
                return Task.FromResult(Result.Failure<CheckoutSessionResult>(
                    PaymentErrors.CheckoutSessionCreationFailed));
            }

            return Task.FromResult<Result<CheckoutSessionResult>>(
                new CheckoutSessionResult(
                    "cs_test_session",
                    "https://checkout.stripe.test/session",
                    "pi_test_intent"));
        }
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
