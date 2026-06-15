using Application.Abstractions.Authentication;
using Application.Events.DeleteEvent;
using Application.Events.ListMyEvents;
using Application.Events.Services;
using Application.Users;
using Application.Users.Services;
using Domain.Events;
using Domain.Events.EventLocations;
using Domain.Events.Services;
using Domain.Users;
using Domain.Users.Services;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApplicationTests.Events.DeleteEvent;

public class DeleteEventCommandHandlerTests
{
    private const string DefaultAvatarUrl = "/images/default-avatar.png";
    private const int MaxPublishesPerWeek = 6;

    [Fact]
    public async Task DeleteEventCommandHandler_Should_SoftDeleteDraft_When_CallerIsEditor()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|delete-success", "delete@example.com");
        var eventId = await SeedDraftEventAsync(databaseName, identity, "Draft To Delete");
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new DeleteEventCommand(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);

        await using var verifyContext = CreateContext(databaseName);
        var deleted = await verifyContext.Events.SingleAsync(e => e.Id == eventId);
        Assert.NotNull(deleted.DeletedAt);
        Assert.True(deleted.IsDeleted);
    }

    [Fact]
    public async Task DeleteEventCommandHandler_Should_ExcludeDeletedDraftFromListMine_When_SoftDeleted()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|delete-list", "list@example.com");
        var eventId = await SeedDraftEventAsync(databaseName, identity, "List Draft");
        await using var context = CreateContext(databaseName);
        var deleteHandler = CreateHandler(context, identity);
        var listHandler = CreateListHandler(context, identity);

        // Act
        var deleteResult = await deleteHandler.Handle(new DeleteEventCommand(eventId), CancellationToken.None);
        var listResult = await listHandler.Handle(new ListMyEventsQuery(), CancellationToken.None);

        // Assert
        Assert.True(deleteResult.IsSuccess);
        Assert.True(listResult.IsSuccess);
        Assert.DoesNotContain(listResult.Value, e => e.Id == eventId);
    }

    [Fact]
    public async Task DeleteEventCommandHandler_Should_ReturnInsufficientPermissions_When_CallerIsNotEditor()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var ownerIdentity = CreateVerifiedIdentity("auth0|delete-owner", "owner@example.com");
        var strangerIdentity = CreateVerifiedIdentity("auth0|delete-stranger", "stranger@example.com");
        var eventId = await SeedDraftEventAsync(databaseName, ownerIdentity, "Protected Draft");
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, strangerIdentity);

        // Act
        var result = await handler.Handle(new DeleteEventCommand(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.InsufficientPermissions", result.Error.Code);
    }

    [Fact]
    public async Task DeleteEventCommandHandler_Should_ReturnDeleted_When_EventAlreadySoftDeleted()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|delete-twice", "twice@example.com");
        var eventId = await SeedSoftDeletedDraftAsync(databaseName, identity);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new DeleteEventCommand(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.Deleted", result.Error.Code);
    }

    [Fact]
    public async Task DeleteEventCommandHandler_Should_ReturnNotDraft_When_EventIsPublished()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|delete-published", "published@example.com");
        var eventId = await SeedPublishedEventAsync(databaseName, identity);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new DeleteEventCommand(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.NotDraft", result.Error.Code);
    }

    [Fact]
    public async Task DeleteEventCommandHandler_Should_ReturnNotDraft_When_EventIsCancelled()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|delete-cancelled", "cancelled@example.com");
        var eventId = await SeedCancelledEventAsync(databaseName, identity);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new DeleteEventCommand(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.NotDraft", result.Error.Code);
    }

    private static async Task<Guid> SeedSoftDeletedDraftAsync(
        string databaseName,
        FakeUserIdentityAccessor identity)
    {
        var eventId = await SeedDraftEventAsync(databaseName, identity, "Already Deleted");
        await using var context = CreateContext(databaseName);
        var @event = await context.Events.SingleAsync(e => e.Id == eventId);
        EventService.SoftDelete(@event);
        await context.SaveChangesAsync(CancellationToken.None);
        return eventId;
    }

    private static async Task<Guid> SeedCancelledEventAsync(
        string databaseName,
        FakeUserIdentityAccessor identity)
    {
        var eventId = await SeedPublishedEventAsync(databaseName, identity);
        await using var context = CreateContext(databaseName);
        var @event = await context.Events.SingleAsync(e => e.Id == eventId);
        EventService.Cancel(@event);
        await context.SaveChangesAsync(CancellationToken.None);
        return eventId;
    }

    private static async Task<Guid> SeedDraftEventAsync(
        string databaseName,
        FakeUserIdentityAccessor identity,
        string title)
    {
        await using var context = CreateContext(databaseName);
        var currentUserService = new CurrentUserService(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));
        var user = (await currentUserService.GetOrProvisionAsync(CancellationToken.None)).Value;

        var createResult = EventService.Create(EventTier.Small, title, user.Id);
        var @event = createResult.Value.Event;
        var organizer = createResult.Value.Organizer;

        context.Events.Add(@event);
        context.EventOrganizers.Add(organizer);
        await context.SaveChangesAsync(CancellationToken.None);
        return @event.Id;
    }

    private static async Task<Guid> SeedPublishedEventAsync(
        string databaseName,
        FakeUserIdentityAccessor identity)
    {
        await using var context = CreateContext(databaseName);
        var currentUserService = new CurrentUserService(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));
        var user = (await currentUserService.GetOrProvisionAsync(CancellationToken.None)).Value;

        var createResult = EventService.Create(EventTier.Small, "Published Event", user.Id);
        var @event = createResult.Value.Event;
        var organizer = createResult.Value.Organizer;

        var categoryId = SeedCategoryInContext(context);
        MakePublishReadyViaUpdate(@event, categoryId, user.Id);
        EventService.Publish(@event, categoryExists: true, recentPublishCount: 0, maxPublishesPerWeek: MaxPublishesPerWeek);

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
        Guid actingUserId)
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
            AdmissionType = AdmissionType.Free,
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

    private static DeleteEventCommandHandler CreateHandler(
        ApplicationDbContext context,
        IUserIdentityAccessor identity)
    {
        var currentUserService = new CurrentUserService(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));

        return new DeleteEventCommandHandler(
            context,
            currentUserService,
            identity,
            new EventAccessService(context));
    }

    private static ListMyEventsQueryHandler CreateListHandler(
        ApplicationDbContext context,
        IUserIdentityAccessor identity)
    {
        var currentUserService = new CurrentUserService(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));

        return new ListMyEventsQueryHandler(
            context,
            currentUserService,
            identity,
            new EventAccessService(context));
    }

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
