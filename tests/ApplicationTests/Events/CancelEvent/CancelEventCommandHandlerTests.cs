using Application.Abstractions.Authentication;
using Application.Events.CancelEvent;
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

namespace ApplicationTests.Events.CancelEvent;

using ApplicationTests.Events;

public class CancelEventCommandHandlerTests
{
    private const string DefaultAvatarUrl = "/images/default-avatar.png";
    private const int MaxPublishesPerWeek = 6;

    [Fact]
    public async Task CancelEventCommandHandler_Should_CancelPublishedEvent_When_CallerIsEditor()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|cancel-success", "cancel@example.com");
        var eventId = await SeedPublishedEventAsync(databaseName, identity);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new CancelEventCommand(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);

        await using var verifyContext = CreateContext(databaseName);
        var cancelled = await verifyContext.Events.SingleAsync(e => e.Id == eventId);
        Assert.Equal(EventStatus.Cancelled, cancelled.Status);
    }

    [Fact]
    public async Task CancelEventCommandHandler_Should_ReturnInsufficientPermissions_When_CallerIsNotEditor()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var ownerIdentity = CreateVerifiedIdentity("auth0|cancel-owner", "owner@example.com");
        var strangerIdentity = CreateVerifiedIdentity("auth0|cancel-stranger", "stranger@example.com");
        var eventId = await SeedPublishedEventAsync(databaseName, ownerIdentity);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, strangerIdentity);

        // Act
        var result = await handler.Handle(new CancelEventCommand(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.InsufficientPermissions", result.Error.Code);
    }

    [Fact]
    public async Task CancelEventCommandHandler_Should_ReturnDeleted_When_EventIsSoftDeleted()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|cancel-deleted", "deleted@example.com");
        var eventId = await SeedPublishReadyDraftAsync(databaseName, identity, softDeleted: true);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new CancelEventCommand(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.Deleted", result.Error.Code);
    }

    [Fact]
    public async Task CancelEventCommandHandler_Should_ReturnNotPublished_When_EventIsDraft()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|cancel-draft", "draft@example.com");
        var eventId = await SeedDraftEventAsync(databaseName, identity, "Draft Event");
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new CancelEventCommand(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.NotPublished", result.Error.Code);
    }

    [Fact]
    public async Task CancelEventCommandHandler_Should_ReturnNotPublished_When_EventAlreadyCancelled()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|cancel-twice", "twice@example.com");
        var eventId = await SeedCancelledEventAsync(databaseName, identity);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new CancelEventCommand(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.NotPublished", result.Error.Code);
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

    private static async Task<Guid> SeedPublishReadyDraftAsync(
        string databaseName,
        FakeUserIdentityAccessor identity,
        bool softDeleted = false)
    {
        await using var context = CreateContext(databaseName);
        var currentUserService = new CurrentUserService(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));
        var user = (await currentUserService.GetOrProvisionAsync(CancellationToken.None)).Value;

        var createResult = EventService.Create(EventTier.Small, "Draft Ready", user.Id, EventTestConstants.DefaultBannerUrl);
        var @event = createResult.Value.Event;
        var organizer = createResult.Value.Organizer;

        var categoryId = SeedCategoryInContext(context);
        MakePublishReadyViaUpdate(@event, categoryId, user.Id);

        if (softDeleted)
        {
            EventService.SoftDelete(@event);
        }

        context.Events.Add(@event);
        context.EventOrganizers.Add(organizer);
        await context.SaveChangesAsync(CancellationToken.None);
        return @event.Id;
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

        var createResult = EventService.Create(EventTier.Small, title, user.Id, EventTestConstants.DefaultBannerUrl);
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

        var createResult = EventService.Create(EventTier.Small, "Published Event", user.Id, EventTestConstants.DefaultBannerUrl);
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
        Guid actingUserId,
        AdmissionType admissionType = AdmissionType.Free)
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

    private static CancelEventCommandHandler CreateHandler(
        ApplicationDbContext context,
        IUserIdentityAccessor identity)
    {
        var currentUserService = new CurrentUserService(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));

        return new CancelEventCommandHandler(
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
