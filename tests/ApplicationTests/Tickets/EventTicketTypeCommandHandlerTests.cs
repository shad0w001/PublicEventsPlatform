using Application.Abstractions.Authentication;
using Application.Events.Services;
using Application.Tickets.CreateEventTicketType;
using Application.Tickets.DeleteEventTicketType;
using Application.Tickets.UpdateEventTicketType;
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

namespace ApplicationTests.Tickets;

using ApplicationTests.Events;

public class EventTicketTypeCommandHandlerTests
{
    private const string DefaultAvatarUrl = "/images/default-avatar.png";
    private const string DefaultGroupImageUrl = "/images/default-group.png";

    [Fact]
    public async Task CreateEventTicketTypeCommandHandler_Should_ReturnTicketTypeResponse_When_PaidDraftEvent()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|ticket-create", "ticket-create@example.com");
        var eventId = await SeedPaidDraftEventAsync(databaseName, identity);
        await using var context = CreateContext(databaseName);
        var handler = CreateCreateHandler(context, identity);

        // Act
        var result = await handler.Handle(
            new CreateEventTicketTypeCommand(eventId, "VIP", "VIP access", 5000, 50),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("VIP", result.Value.Name);
        Assert.Equal(5000, result.Value.PriceCents);
        Assert.Equal(50, result.Value.Capacity);
        Assert.Equal(50, result.Value.RemainingQuantity);
    }

    [Fact]
    public async Task CreateEventTicketTypeCommandHandler_Should_ReturnPaidAdmissionRequired_When_EventIsFree()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|ticket-free", "ticket-free@example.com");
        var eventId = await SeedPaidDraftEventAsync(databaseName, identity, admissionType: AdmissionType.Free);
        await using var context = CreateContext(databaseName);
        var handler = CreateCreateHandler(context, identity);

        // Act
        var result = await handler.Handle(
            new CreateEventTicketTypeCommand(eventId, "VIP", null, 5000, 50),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(TicketErrors.PaidAdmissionRequired.Code, result.Error.Code);
    }

    [Fact]
    public async Task CreateEventTicketTypeCommandHandler_Should_ReturnCannotModifyCancelled_When_EventIsCancelled()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|ticket-cancelled", "ticket-cancelled@example.com");
        var eventId = await SeedPublishedPaidEventAsync(databaseName, identity, cancelled: true);
        await using var context = CreateContext(databaseName);
        var handler = CreateCreateHandler(context, identity);

        // Act
        var result = await handler.Handle(
            new CreateEventTicketTypeCommand(eventId, "Late Add", null, 2500, 10),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(EventErrors.CannotModifyCancelled.Code, result.Error.Code);
    }

    [Fact]
    public async Task CreateEventTicketTypeCommandHandler_Should_ReturnInsufficientPermissions_When_NonEditorCreates()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var ownerIdentity = CreateVerifiedIdentity("auth0|ticket-owner", "ticket-owner@example.com");
        var eventId = await SeedPaidDraftEventAsync(databaseName, ownerIdentity);
        var strangerIdentity = CreateVerifiedIdentity("auth0|ticket-stranger", "ticket-stranger@example.com");
        await using var context = CreateContext(databaseName);
        var handler = CreateCreateHandler(context, strangerIdentity);

        // Act
        var result = await handler.Handle(
            new CreateEventTicketTypeCommand(eventId, "VIP", null, 5000, 50),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.InsufficientPermissions", result.Error.Code);
    }

    [Fact]
    public async Task UpdateEventTicketTypeCommandHandler_Should_UpdatePrice_When_NoSalesExist()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|ticket-update-price", "ticket-update-price@example.com");
        var (eventId, ticketTypeId) = await SeedPaidDraftWithTicketTypeAsync(databaseName, identity);
        await using var context = CreateContext(databaseName);
        var handler = CreateUpdateHandler(context, identity);

        // Act
        var result = await handler.Handle(
            new UpdateEventTicketTypeCommand(eventId, ticketTypeId, null, null, 3500, null),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(3500, result.Value.PriceCents);
    }

    [Fact]
    public async Task UpdateEventTicketTypeCommandHandler_Should_ReturnPriceImmutableAfterSales_When_PriceChangedAfterSale()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|ticket-price-lock", "ticket-price-lock@example.com");
        var (eventId, ticketTypeId) = await SeedPaidDraftWithTicketTypeAsync(databaseName, identity);
        await using (var seedContext = CreateContext(databaseName))
        {
            var ticketType = await seedContext.TicketTypes.SingleAsync(t => t.Id == ticketTypeId);
            ticketType.SoldQuantity = 1;
            await seedContext.SaveChangesAsync();
        }

        await using var context = CreateContext(databaseName);
        var handler = CreateUpdateHandler(context, identity);

        // Act
        var result = await handler.Handle(
            new UpdateEventTicketTypeCommand(eventId, ticketTypeId, null, null, 3500, null),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(TicketErrors.PriceImmutableAfterSales.Code, result.Error.Code);
    }

    [Fact]
    public async Task UpdateEventTicketTypeCommandHandler_Should_ReturnTicketTypeNotFound_When_TypeMissing()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|ticket-update-missing", "ticket-update-missing@example.com");
        var eventId = await SeedPaidDraftEventAsync(databaseName, identity);
        await using var context = CreateContext(databaseName);
        var handler = CreateUpdateHandler(context, identity);
        var missingId = Guid.Parse("99999999-9999-9999-9999-999999999999");

        // Act
        var result = await handler.Handle(
            new UpdateEventTicketTypeCommand(eventId, missingId, "New Name", null, null, null),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(TicketErrors.TicketTypeNotFound(missingId).Code, result.Error.Code);
    }

    [Fact]
    public async Task DeleteEventTicketTypeCommandHandler_Should_DeleteTicketType_When_NoSalesOrReservations()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|ticket-delete", "ticket-delete@example.com");
        var (eventId, ticketTypeId) = await SeedPaidDraftWithTicketTypeAsync(databaseName, identity);
        await using var context = CreateContext(databaseName);
        var handler = CreateDeleteHandler(context, identity);

        // Act
        var result = await handler.Handle(
            new DeleteEventTicketTypeCommand(eventId, ticketTypeId),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        await using var verifyContext = CreateContext(databaseName);
        Assert.False(await verifyContext.TicketTypes.AnyAsync());
    }

    [Fact]
    public async Task DeleteEventTicketTypeCommandHandler_Should_ReturnCannotDeleteWithSales_When_SoldQuantityPositive()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|ticket-delete-sales", "ticket-delete-sales@example.com");
        var (eventId, ticketTypeId) = await SeedPaidDraftWithTicketTypeAsync(databaseName, identity);
        await using (var seedContext = CreateContext(databaseName))
        {
            var ticketType = await seedContext.TicketTypes.SingleAsync(t => t.Id == ticketTypeId);
            ticketType.SoldQuantity = 1;
            await seedContext.SaveChangesAsync();
        }

        await using var context = CreateContext(databaseName);
        var handler = CreateDeleteHandler(context, identity);

        // Act
        var result = await handler.Handle(
            new DeleteEventTicketTypeCommand(eventId, ticketTypeId),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(TicketErrors.CannotDeleteWithSales.Code, result.Error.Code);
    }

    private static async Task<Guid> SeedPaidDraftEventAsync(
        string databaseName,
        FakeUserIdentityAccessor identity,
        AdmissionType admissionType = AdmissionType.Paid)
    {
        await using var context = CreateContext(databaseName);
        var currentUserService = new CurrentUserService(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));
        var user = (await currentUserService.GetOrProvisionAsync(CancellationToken.None)).Value;

        var createResult = EventService.Create(EventTier.Small, "Paid Ticket Event", user.Id, EventTestConstants.DefaultBannerUrl);
        var @event = createResult.Value.Event;
        var organizer = createResult.Value.Organizer;
        var categoryId = SeedCategoryInContext(context);
        MakePublishReadyViaUpdate(@event, categoryId, user.Id, admissionType);

        context.Events.Add(@event);
        context.EventOrganizers.Add(organizer);
        await context.SaveChangesAsync(CancellationToken.None);
        return @event.Id;
    }

    private static async Task<(Guid EventId, Guid TicketTypeId)> SeedPaidDraftWithTicketTypeAsync(
        string databaseName,
        FakeUserIdentityAccessor identity)
    {
        await using var context = CreateContext(databaseName);
        var currentUserService = new CurrentUserService(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));
        var user = (await currentUserService.GetOrProvisionAsync(CancellationToken.None)).Value;

        var createResult = EventService.Create(EventTier.Small, "Paid With Type", user.Id, EventTestConstants.DefaultBannerUrl);
        var @event = createResult.Value.Event;
        var organizer = createResult.Value.Organizer;
        var categoryId = SeedCategoryInContext(context);
        MakePublishReadyViaUpdate(@event, categoryId, user.Id, AdmissionType.Paid);
        var ticketType = TicketTypeService.Create(@event, "General", "Entry", 2500, 100).Value;

        context.Events.Add(@event);
        context.EventOrganizers.Add(organizer);
        await context.SaveChangesAsync(CancellationToken.None);
        return (@event.Id, ticketType.Id);
    }

    private static async Task<Guid> SeedPublishedPaidEventAsync(
        string databaseName,
        FakeUserIdentityAccessor identity,
        bool cancelled = false)
    {
        await using var context = CreateContext(databaseName);
        var currentUserService = new CurrentUserService(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));
        var user = (await currentUserService.GetOrProvisionAsync(CancellationToken.None)).Value;

        var createResult = EventService.Create(EventTier.Small, "Published Paid", user.Id, EventTestConstants.DefaultBannerUrl);
        var @event = createResult.Value.Event;
        var organizer = createResult.Value.Organizer;
        var categoryId = SeedCategoryInContext(context);
        MakePublishReadyViaUpdate(@event, categoryId, user.Id, AdmissionType.Paid);
        TicketTypeService.Create(@event, "General", "Entry", 2500, 100);
        EventService.Publish(@event, categoryExists: true, recentPublishCount: 0, maxPublishesPerWeek: 6);

        if (cancelled)
        {
            EventService.Cancel(@event);
        }

        context.Events.Add(@event);
        context.EventOrganizers.Add(organizer);
        await context.SaveChangesAsync(CancellationToken.None);
        return @event.Id;
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

    private static CreateEventTicketTypeCommandHandler CreateCreateHandler(
        ApplicationDbContext context,
        IUserIdentityAccessor identity) =>
        new(
            context,
            new CurrentUserService(context, identity, Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl })),
            identity,
            new EventAccessService(context));

    private static UpdateEventTicketTypeCommandHandler CreateUpdateHandler(
        ApplicationDbContext context,
        IUserIdentityAccessor identity) =>
        new(
            context,
            new CurrentUserService(context, identity, Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl })),
            identity,
            new EventAccessService(context));

    private static DeleteEventTicketTypeCommandHandler CreateDeleteHandler(
        ApplicationDbContext context,
        IUserIdentityAccessor identity) =>
        new(
            context,
            new CurrentUserService(context, identity, Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl })),
            identity,
            new EventAccessService(context));

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
}
