using Application.Events.Services;
using Application.Tickets.ValidateEventTicket;
using Application.Users;
using Application.Users.Services;
using Domain.Events;
using Domain.Events.EventLocations;
using Domain.Events.Services;
using Domain.Tickets;
using Domain.Tickets.Services;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace ApplicationTests.Tickets;

public class ValidateEventTicketCommandHandlerTests
{
    [Fact]
    public async Task ValidateEventTicketCommandHandler_Should_ReturnValid_When_FirstQrScanDuringWindow()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var hostIdentity = TicketQueryTestHelper.CreateVerifiedIdentity("auth0|val-host", "host@example.com");
        var seed = await SeedValidationScenarioAsync(databaseName, hostIdentity, quantity: 1);
        await using var context = TicketQueryTestHelper.CreateContext(databaseName);
        var handler = CreateHandler(context, hostIdentity);

        // Act
        var result = await handler.Handle(
            new ValidateEventTicketCommand(seed.EventId, seed.TicketIds[0].ToString(), TicketValidationMethod.QrScan),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(TicketValidationStatus.Valid, result.Value.Status);
        await using var verifyContext = TicketQueryTestHelper.CreateContext(databaseName);
        var validationCount = await verifyContext.TicketValidations.CountAsync(v => v.TicketId == seed.TicketIds[0]);
        Assert.Equal(1, validationCount);
    }

    [Fact]
    public async Task ValidateEventTicketCommandHandler_Should_ReturnAlreadyUsed_When_TicketAlreadyCheckedIn()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var hostIdentity = TicketQueryTestHelper.CreateVerifiedIdentity("auth0|val-reuse", "reuse@example.com");
        var seed = await SeedValidationScenarioAsync(databaseName, hostIdentity, quantity: 1);
        await TicketQueryTestHelper.MarkTicketCheckedInAsync(databaseName, seed.TicketIds[0], seed.BuyerParticipantId);
        await using var context = TicketQueryTestHelper.CreateContext(databaseName);
        var handler = CreateHandler(context, hostIdentity);

        // Act
        var result = await handler.Handle(
            new ValidateEventTicketCommand(seed.EventId, seed.TicketIds[0].ToString(), TicketValidationMethod.QrScan),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(TicketValidationStatus.AlreadyUsed, result.Value.Status);
    }

    [Fact]
    public async Task ValidateEventTicketCommandHandler_Should_ReturnInvalid_When_UnknownUuid()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var hostIdentity = TicketQueryTestHelper.CreateVerifiedIdentity("auth0|val-unknown", "unknown@example.com");
        var seed = await SeedValidationScenarioAsync(databaseName, hostIdentity);
        await using var context = TicketQueryTestHelper.CreateContext(databaseName);
        var handler = CreateHandler(context, hostIdentity);
        var randomId = Guid.NewGuid();

        // Act
        var result = await handler.Handle(
            new ValidateEventTicketCommand(seed.EventId, randomId.ToString(), TicketValidationMethod.QrScan),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(TicketValidationStatus.Invalid, result.Value.Status);
        await using var verifyContext = TicketQueryTestHelper.CreateContext(databaseName);
        var auditCount = await verifyContext.TicketValidations.CountAsync(v => v.TicketId == randomId);
        Assert.Equal(0, auditCount);
    }

    [Fact]
    public async Task ValidateEventTicketCommandHandler_Should_ReturnInvalid_When_UnknownManualCode()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var hostIdentity = TicketQueryTestHelper.CreateVerifiedIdentity("auth0|val-badcode", "badcode@example.com");
        var seed = await SeedValidationScenarioAsync(databaseName, hostIdentity);
        await using var context = TicketQueryTestHelper.CreateContext(databaseName);
        var handler = CreateHandler(context, hostIdentity);

        // Act
        var result = await handler.Handle(
            new ValidateEventTicketCommand(seed.EventId, "ZZZZZZZZ", TicketValidationMethod.ManualEntry),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(TicketValidationStatus.Invalid, result.Value.Status);
    }

    [Fact]
    public async Task ValidateEventTicketCommandHandler_Should_ReturnValid_When_ManualCodeMatchesCaseInsensitively()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var hostIdentity = TicketQueryTestHelper.CreateVerifiedIdentity("auth0|val-manual", "manual@example.com");
        var seed = await SeedValidationScenarioAsync(databaseName, hostIdentity, quantity: 1);
        await using var context = TicketQueryTestHelper.CreateContext(databaseName);
        var handler = CreateHandler(context, hostIdentity);

        // Act
        var result = await handler.Handle(
            new ValidateEventTicketCommand(seed.EventId, seed.ManualCode.ToLowerInvariant(), TicketValidationMethod.ManualEntry),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(TicketValidationStatus.Valid, result.Value.Status);
    }

    [Fact]
    public async Task ValidateEventTicketCommandHandler_Should_ReturnInvalid_When_TicketForDifferentEvent()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var hostIdentity = TicketQueryTestHelper.CreateVerifiedIdentity("auth0|val-wrongevent", "wrongevent@example.com");
        var seed = await SeedValidationScenarioAsync(databaseName, hostIdentity, quantity: 1);
        var otherSeed = await SeedValidationScenarioAsync(databaseName, hostIdentity, quantity: 1);
        await using var context = TicketQueryTestHelper.CreateContext(databaseName);
        var handler = CreateHandler(context, hostIdentity);

        // Act — validate a ticket from seed against otherSeed's event
        var result = await handler.Handle(
            new ValidateEventTicketCommand(otherSeed.EventId, seed.TicketIds[0].ToString(), TicketValidationMethod.QrScan),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(TicketValidationStatus.Invalid, result.Value.Status);
    }

    [Fact]
    public async Task ValidateEventTicketCommandHandler_Should_ReturnForbidden_When_NotEventHost()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var hostIdentity = TicketQueryTestHelper.CreateVerifiedIdentity("auth0|val-host2", "host2@example.com");
        var strangerIdentity = TicketQueryTestHelper.CreateVerifiedIdentity("auth0|val-stranger", "stranger@example.com");
        var seed = await SeedValidationScenarioAsync(databaseName, hostIdentity, quantity: 1);
        await ProvisionUserAsync(databaseName, strangerIdentity);
        await using var context = TicketQueryTestHelper.CreateContext(databaseName);
        var handler = CreateHandler(context, strangerIdentity);

        // Act
        var result = await handler.Handle(
            new ValidateEventTicketCommand(seed.EventId, seed.TicketIds[0].ToString(), TicketValidationMethod.QrScan),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Forbidden, result.Error.Type);
    }

    [Fact]
    public async Task ValidateEventTicketCommandHandler_Should_ReturnConflict_When_EventIsDraft()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var hostIdentity = TicketQueryTestHelper.CreateVerifiedIdentity("auth0|val-draft", "draft@example.com");
        var eventId = await SeedDraftPaidEventAsync(databaseName, hostIdentity);
        await using var context = TicketQueryTestHelper.CreateContext(databaseName);
        var handler = CreateHandler(context, hostIdentity);

        // Act
        var result = await handler.Handle(
            new ValidateEventTicketCommand(eventId, Guid.NewGuid().ToString(), TicketValidationMethod.QrScan),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
    }

    private static ValidateEventTicketCommandHandler CreateHandler(
        ApplicationDbContext context,
        TicketQueryTestHelper.FakeUserIdentityAccessor identity) =>
        new(
            context,
            new CurrentUserService(context, identity, Options.Create(new UserProfileOptions { DefaultAvatarUrl = TicketQueryTestHelper.DefaultAvatarUrl })),
            identity,
            new EventAccessService(context),
            new TicketRowLock(context));

    private static async Task<TicketQueryTestHelper.PaidOrderSeed> SeedValidationScenarioAsync(
        string databaseName,
        TicketQueryTestHelper.FakeUserIdentityAccessor hostIdentity,
        int quantity = 2)
    {
        await using var context = TicketQueryTestHelper.CreateContext(databaseName);
        var hostService = new CurrentUserService(
            context,
            hostIdentity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = TicketQueryTestHelper.DefaultAvatarUrl }));
        var host = (await hostService.GetOrProvisionAsync(CancellationToken.None)).Value;

        var now = DateTime.UtcNow;
        var createResult = EventService.Create(EventTier.Small, "Validation Event", host.Id);
        var @event = createResult.Value.Event;
        var organizer = createResult.Value.Organizer;
        var categoryId = SeedCategoryInContext(context);

        var patch = new EventUpdatePatch
        {
            Description = "A test event for door validation",
            CategoryId = categoryId,
            StartTime = now.AddHours(-1),
            EndTime = now.AddHours(3),
            TimeZoneId = "Europe/Sofia",
            AdmissionType = AdmissionType.Paid,
            Locations =
            [
                new EventLocation
                {
                    Name = "Main Entrance",
                    Kind = EventLocationKind.Physical,
                    Address = "1 Main St",
                    City = "Sofia"
                }
            ]
        };

        EventService.Update(@event, patch, host.Id);
        var ticketType = TicketTypeService.Create(@event, "General", "Entry", 1000, 100).Value;
        EventService.Publish(@event, categoryExists: true, recentPublishCount: 0, maxPublishesPerWeek: 6);

        var order = OrderService.CreatePending(@event, ticketType, host.Id, quantity, now.AddMinutes(30)).Value;
        OrderService.ReserveInventory(ticketType, quantity);
        OrderService.MarkPaid(order, ticketType, quantity);
        var tickets = TicketService.IssueTickets(order, ticketType, host.Id, quantity).Value;

        context.Events.Add(@event);
        context.EventOrganizers.Add(organizer);
        context.Orders.Add(order);
        context.Tickets.AddRange(tickets);
        await context.SaveChangesAsync(CancellationToken.None);

        return new TicketQueryTestHelper.PaidOrderSeed(
            order.Id,
            @event.Id,
            ticketType.Id,
            host.Id,
            tickets.Select(t => t.Id).ToList(),
            tickets[0].TicketCode!.ManualCode);
    }

    private static async Task<Guid> SeedDraftPaidEventAsync(
        string databaseName,
        TicketQueryTestHelper.FakeUserIdentityAccessor hostIdentity)
    {
        await using var context = TicketQueryTestHelper.CreateContext(databaseName);
        var hostService = new CurrentUserService(
            context,
            hostIdentity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = TicketQueryTestHelper.DefaultAvatarUrl }));
        var host = (await hostService.GetOrProvisionAsync(CancellationToken.None)).Value;

        var createResult = EventService.Create(EventTier.Small, "Draft Event", host.Id);
        var @event = createResult.Value.Event;
        var organizer = createResult.Value.Organizer;
        @event.AdmissionType = AdmissionType.Paid;

        context.Events.Add(@event);
        context.EventOrganizers.Add(organizer);
        await context.SaveChangesAsync(CancellationToken.None);

        return @event.Id;
    }

    private static async Task ProvisionUserAsync(
        string databaseName,
        TicketQueryTestHelper.FakeUserIdentityAccessor identity)
    {
        await using var context = TicketQueryTestHelper.CreateContext(databaseName);
        var service = new CurrentUserService(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = TicketQueryTestHelper.DefaultAvatarUrl }));
        await service.GetOrProvisionAsync(CancellationToken.None);
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
}
