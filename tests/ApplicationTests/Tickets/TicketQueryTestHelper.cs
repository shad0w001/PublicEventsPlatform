using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Events.Services;
using Application.Tickets.GetOrder;
using Application.Tickets.GetTicket;
using Application.Tickets.ListMyTickets;
using Application.Tickets.ProcessStripeWebhook;
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
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace ApplicationTests.Tickets;

internal static class TicketQueryTestHelper
{
    internal const string DefaultAvatarUrl = "/images/default-avatar.png";
    internal const string DefaultGroupImageUrl = "/images/default-group.png";
    internal const string CheckoutSessionId = "cs_test_read_api";

    internal static async Task<PaidOrderSeed> SeedPaidOrderWithTicketsAsync(
        string databaseName,
        FakeUserIdentityAccessor buyerIdentity,
        int quantity = 2,
        bool cancelledEvent = false)
    {
        await using var context = CreateContext(databaseName);
        var buyerService = new CurrentUserService(
            context,
            buyerIdentity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));
        var buyer = (await buyerService.GetOrProvisionAsync(CancellationToken.None)).Value;

        var createResult = EventService.Create(EventTier.Small, "Paid Event", buyer.Id);
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
            quantity,
            DateTime.UtcNow.AddMinutes(30));
        var order = orderResult.Value;
        OrderService.ReserveInventory(ticketType, quantity);
        OrderService.AttachCheckoutSession(order, CheckoutSessionId, null);
        OrderService.MarkPaid(order, ticketType, quantity);
        var tickets = TicketService.IssueTickets(order, ticketType, buyer.Id, quantity).Value;

        if (cancelledEvent)
        {
            EventService.Cancel(@event);
        }

        context.Events.Add(@event);
        context.EventOrganizers.Add(organizer);
        context.Orders.Add(order);
        context.Tickets.AddRange(tickets);
        await context.SaveChangesAsync(CancellationToken.None);

        return new PaidOrderSeed(
            order.Id,
            @event.Id,
            ticketType.Id,
            buyer.Id,
            tickets.Select(t => t.Id).ToList(),
            tickets[0].TicketCode!.ManualCode);
    }

    internal static async Task<(PaidOrderSeed Seed, Guid GroupId)> SeedGroupPaidOrderAsync(
        string databaseName,
        FakeUserIdentityAccessor ownerIdentity,
        FakeUserIdentityAccessor organizerIdentity,
        int quantity = 1)
    {
        await using var context = CreateContext(databaseName);
        var ownerService = new CurrentUserService(
            context,
            ownerIdentity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));
        var owner = (await ownerService.GetOrProvisionAsync(CancellationToken.None)).Value;

        var organizerService = new CurrentUserService(
            context,
            organizerIdentity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));
        var organizer = (await organizerService.GetOrProvisionAsync(CancellationToken.None)).Value;

        var groupCreate = GroupService.Create(
            "Ticket Org",
            "",
            GroupJoinPolicy.Open,
            owner.Id,
            DefaultGroupImageUrl);
        var group = groupCreate.Value.Group;
        var organizerMembership = GroupMembership.Create(group.Id, organizer.Id, GroupMemberRole.Organizer);

        var createResult = EventService.Create(EventTier.Small, "Group Paid Event", owner.Id);
        var @event = createResult.Value.Event;
        var eventOrganizer = createResult.Value.Organizer;
        var categoryId = SeedCategoryInContext(context);
        MakePublishReadyViaUpdate(@event, categoryId, owner.Id, AdmissionType.Paid);
        var ticketType = TicketTypeService.Create(@event, "VIP", "Entry", 5000, 50).Value;
        EventService.Publish(@event, categoryExists: true, recentPublishCount: 0, maxPublishesPerWeek: 6);

        var orderResult = OrderService.CreatePending(
            @event,
            ticketType,
            group.Id,
            quantity,
            DateTime.UtcNow.AddMinutes(30));
        var order = orderResult.Value;
        OrderService.ReserveInventory(ticketType, quantity);
        OrderService.MarkPaid(order, ticketType, quantity);
        var tickets = TicketService.IssueTickets(order, ticketType, group.Id, quantity).Value;

        context.Groups.Add(group);
        context.GroupMemberships.Add(groupCreate.Value.OwnerMembership);
        context.GroupMemberships.Add(organizerMembership);
        context.Events.Add(@event);
        context.EventOrganizers.Add(eventOrganizer);
        context.Orders.Add(order);
        context.Tickets.AddRange(tickets);
        await context.SaveChangesAsync(CancellationToken.None);

        var seed = new PaidOrderSeed(
            order.Id,
            @event.Id,
            ticketType.Id,
            group.Id,
            tickets.Select(t => t.Id).ToList(),
            tickets[0].TicketCode!.ManualCode);

        return (seed, group.Id);
    }

    internal static async Task MarkTicketCheckedInAsync(string databaseName, Guid ticketId, Guid validatorUserId)
    {
        await using var context = CreateContext(databaseName);
        var ticket = await context.Tickets
            .Include(t => t.Validations)
            .SingleAsync(t => t.Id == ticketId);

        context.TicketValidations.Add(new TicketValidation
        {
            TicketId = ticket.Id,
            ValidatedByUserId = validatorUserId,
            Method = TicketValidationMethod.QrScan,
            Status = TicketValidationStatus.Valid
        });
        await context.SaveChangesAsync(CancellationToken.None);
    }

    internal static GetOrderQueryHandler CreateGetOrderHandler(
        ApplicationDbContext context,
        FakeUserIdentityAccessor identity) =>
        new(
            context,
            new CurrentUserService(context, identity, Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl })),
            identity,
            new EventAccessService(context));

    internal static ListMyTicketsQueryHandler CreateListMyTicketsHandler(
        ApplicationDbContext context,
        FakeUserIdentityAccessor identity) =>
        new(
            context,
            new CurrentUserService(context, identity, Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl })),
            identity,
            new EventAccessService(context));

    internal static GetTicketQueryHandler CreateGetTicketHandler(
        ApplicationDbContext context,
        FakeUserIdentityAccessor identity) =>
        new(
            context,
            new CurrentUserService(context, identity, Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl })),
            identity,
            new EventAccessService(context));

    internal static FakeUserIdentityAccessor CreateVerifiedIdentity(string sub, string email) =>
        new()
        {
            IsAuthenticated = true,
            ExternalSubjectId = sub,
            Email = email,
            EmailVerified = true,
            ServiceRole = ServiceRole.User
        };

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
        var patch = new EventUpdatePatch
        {
            Description = "A test event description",
            CategoryId = categoryId,
            StartTime = new DateTime(2026, 7, 1, 18, 0, 0, DateTimeKind.Utc),
            EndTime = new DateTime(2026, 7, 1, 22, 0, 0, DateTimeKind.Utc),
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

    internal static ApplicationDbContext CreateContext(string databaseName) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options);

    internal sealed record PaidOrderSeed(
        Guid OrderId,
        Guid EventId,
        Guid TicketTypeId,
        Guid BuyerParticipantId,
        IReadOnlyList<Guid> TicketIds,
        string ManualCode);

    internal sealed class FakeUserIdentityAccessor : IUserIdentityAccessor
    {
        public bool IsAuthenticated { get; init; }
        public string ExternalSubjectId { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public bool EmailVerified { get; init; }
        public ServiceRole ServiceRole { get; init; }
        public string? ProfilePictureUrl { get; init; }
    }
}
