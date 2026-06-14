using Application.Abstractions.Authentication;
using Application.Events;
using Application.Events.CreateEvent;
using Application.Events.UpdateEvent;
using Application.Events.Services;
using Application.Users;
using Application.Users.Services;
using Domain.Events;
using Domain.Events.EventLocations;
using Domain.Events.Services;
using Domain.Groups;
using Domain.Groups.Services;
using Domain.Users;
using Domain.Users.Services;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApplicationTests.Events.UpdateEvent;

public class UpdateEventCommandHandlerTests
{
    private const string DefaultAvatarUrl = "/images/default-avatar.png";
    private const string DefaultGroupImageUrl = "/images/default-group.png";

    [Fact]
    public async Task UpdateEventCommandHandler_Should_SetCreatedByUserId_When_FirstPatchApplied()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|update-first-patch", "first@example.com");
        var eventId = await SeedDraftEventAsync(databaseName, identity, "Original Title");
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        var command = new UpdateEventCommand(eventId, Description: "Event description");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.CreatedByUserId);

        await using var verifyContext = CreateContext(databaseName);
        var user = await verifyContext.Users.SingleAsync();
        var persistedEvent = await verifyContext.Events.SingleAsync();
        Assert.Equal(user.Id, persistedEvent.CreatedByUserId);
        Assert.Equal(user.Id, result.Value.CreatedByUserId);
    }

    [Fact]
    public async Task UpdateEventCommandHandler_Should_LeaveTitleUnchanged_When_OnlyDescriptionAndCategoryPatched()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|update-partial", "partial@example.com");
        var eventId = await SeedDraftEventAsync(databaseName, identity, "Keep This Title");
        var categoryId = await SeedCategoryAsync(databaseName);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        var command = new UpdateEventCommand(
            eventId,
            Description: "Screen 2 description",
            CategoryId: categoryId);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Keep This Title", result.Value.Title);
        Assert.Equal("Screen 2 description", result.Value.Description);
        Assert.Equal(categoryId, result.Value.CategoryId);
        Assert.Equal("Music", result.Value.CategoryName);
    }

    [Fact]
    public async Task UpdateEventCommandHandler_Should_ReplaceAllLocations_When_LocationsProvided()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|update-locations", "locations@example.com");
        var eventId = await SeedDraftEventAsync(databaseName, identity, "Location Event");
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        var segmentDate = new DateTime(2026, 8, 1, 18, 0, 0, DateTimeKind.Utc);
        var locations = new List<EventLocationResponse>
        {
            new("Hall A", segmentDate, EventLocationKind.Physical, null, "1 Main St", null, null, "Sofia", "BG", null),
            new("Live Stream", segmentDate, EventLocationKind.Virtual, "https://stream.example.com", null, null, null, null, null, null)
        };

        var command = new UpdateEventCommand(eventId, Locations: locations);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Locations.Count);
        Assert.Equal(EventLocationType.Hybrid, result.Value.LocationType);
    }

    [Fact]
    public async Task UpdateEventCommandHandler_Should_ReturnCategoryNotFound_When_CategoryIdDoesNotExist()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|update-bad-category", "badcat@example.com");
        var eventId = await SeedDraftEventAsync(databaseName, identity, "Category Test");
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        var missingCategoryId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        var command = new UpdateEventCommand(eventId, CategoryId: missingCategoryId);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.CategoryNotFound", result.Error.Code);
    }

    [Fact]
    public async Task UpdateEventCommandHandler_Should_ReturnInsufficientPermissions_When_NonEditorPatches()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var ownerIdentity = CreateVerifiedIdentity("auth0|update-owner", "owner@example.com");
        var eventId = await SeedDraftEventAsync(databaseName, ownerIdentity, "Private Draft");

        var strangerIdentity = CreateVerifiedIdentity("auth0|update-stranger", "stranger@example.com");
        await using var context = CreateContext(databaseName);
        var strangerHandler = CreateHandler(context, strangerIdentity);

        var command = new UpdateEventCommand(eventId, Title: "Hijacked");

        // Act
        var result = await strangerHandler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.InsufficientPermissions", result.Error.Code);
    }

    [Fact]
    public async Task UpdateEventCommandHandler_Should_ReturnNotFound_When_EventIsSoftDeleted()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|update-deleted", "deleted@example.com");
        var eventId = await SeedDraftEventAsync(databaseName, identity, "Deleted Event", softDeleted: true);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        var command = new UpdateEventCommand(eventId, Title: "Too Late");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.Deleted", result.Error.Code);
    }

    [Fact]
    public async Task UpdateEventCommandHandler_Should_ReturnCannotModifyCancelled_When_EventIsCancelled()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|update-cancelled", "cancelled@example.com");
        var eventId = await SeedPublishedEventAsync(databaseName, identity, cancelled: true);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        var command = new UpdateEventCommand(eventId, Title: "Revive");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.CannotModifyCancelled", result.Error.Code);
    }

    [Fact]
    public async Task UpdateEventCommandHandler_Should_UpdatePublishedEvent_When_CreatorPatches()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|update-published", "published@example.com");
        var eventId = await SeedPublishedEventAsync(databaseName, identity);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        var command = new UpdateEventCommand(eventId, Title: "Updated Published Title");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(EventStatus.Published, result.Value.Status);
        Assert.Equal("Updated Published Title", result.Value.Title);
    }

    [Fact]
    public async Task UpdateEventCommandHandler_Should_UpdateGroupHostedEvent_When_OrganizerPatches()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|org-owner", "owner@org.example.com");
        var organizer = CreateUser("auth0|org-organizer", "organizer@org.example.com");
        var group = SeedGroupWithMember(databaseName, owner, organizer, GroupMemberRole.Organizer);
        var eventId = await SeedDraftEventForHostAsync(
            databaseName,
            CreateVerifiedIdentity("auth0|org-organizer", "organizer@org.example.com"),
            group.Id,
            "Org Event");

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(
            context,
            CreateVerifiedIdentity("auth0|org-organizer", "organizer@org.example.com"));

        var command = new UpdateEventCommand(eventId, Description: "Organizer edit");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Organizer edit", result.Value.Description);
        Assert.True(result.Value.HostIsGroup);
        Assert.Equal(group.Id, result.Value.HostParticipantId);
    }

    [Fact]
    public async Task UpdateEventCommandHandler_Should_ReturnInsufficientPermissions_When_GroupMemberWithoutOrganizerRolePatches()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|member-owner", "owner@member.org");
        var member = CreateUser("auth0|plain-member", "member@org.example.com");
        var group = SeedGroupWithMember(databaseName, owner, member, GroupMemberRole.Member);
        var eventId = await SeedDraftEventForHostAsync(
            databaseName,
            CreateVerifiedIdentity("auth0|member-owner", "owner@member.org"),
            group.Id,
            "Members Cannot Edit");

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(
            context,
            CreateVerifiedIdentity("auth0|plain-member", "member@org.example.com"));

        var command = new UpdateEventCommand(eventId, Description: "Member attempt");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.InsufficientPermissions", result.Error.Code);
    }

    [Fact]
    public async Task UpdateEventCommandHandler_Should_ReturnNoFieldsToUpdate_When_CommandIsEmpty()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|update-empty", "empty@example.com");
        var eventId = await SeedDraftEventAsync(databaseName, identity, "Empty Patch");
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        var command = new UpdateEventCommand(eventId);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.NoFieldsToUpdate", result.Error.Code);
    }

    [Fact]
    public async Task UpdateEventCommandHandler_Should_ReturnEmailNotVerified_When_EmailIsUnverified()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = new FakeUserIdentityAccessor
        {
            IsAuthenticated = true,
            ExternalSubjectId = "auth0|update-unverified",
            Email = "unverified@example.com",
            EmailVerified = false,
            ServiceRole = ServiceRole.User
        };

        var verifiedIdentity = CreateVerifiedIdentity("auth0|update-unverified-host", "host@example.com");
        var eventId = await SeedDraftEventAsync(databaseName, verifiedIdentity, "Gate Test");
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        var command = new UpdateEventCommand(eventId, Title: "Blocked");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Users.EmailNotVerified", result.Error.Code);
    }

    private static async Task<Guid> SeedDraftEventAsync(
        string databaseName,
        FakeUserIdentityAccessor identity,
        string title,
        bool softDeleted = false)
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

        if (softDeleted)
        {
            EventService.SoftDelete(@event);
        }

        context.Events.Add(@event);
        context.EventOrganizers.Add(organizer);
        await context.SaveChangesAsync(CancellationToken.None);
        return @event.Id;
    }

    private static async Task<Guid> SeedDraftEventForHostAsync(
        string databaseName,
        FakeUserIdentityAccessor creatorIdentity,
        Guid hostParticipantId,
        string title)
    {
        await using var context = CreateContext(databaseName);
        var currentUserService = new CurrentUserService(
            context,
            creatorIdentity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));
        await currentUserService.GetOrProvisionAsync(CancellationToken.None);

        var createResult = EventService.Create(EventTier.Small, title, hostParticipantId);
        var @event = createResult.Value.Event;
        var organizer = createResult.Value.Organizer;

        context.Events.Add(@event);
        context.EventOrganizers.Add(organizer);
        await context.SaveChangesAsync(CancellationToken.None);
        return @event.Id;
    }

    private static async Task<Guid> SeedPublishedEventAsync(
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

        var createResult = EventService.Create(EventTier.Small, "Published Event", user.Id);
        var @event = createResult.Value.Event;
        var organizer = createResult.Value.Organizer;

        var categoryId = SeedCategoryInContext(context);
        MakePublishReadyViaUpdate(@event, categoryId, user.Id);
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

    private static async Task<Guid> SeedCategoryAsync(string databaseName)
    {
        await using var context = CreateContext(databaseName);
        var categoryId = SeedCategoryInContext(context);
        await context.SaveChangesAsync(CancellationToken.None);
        return categoryId;
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

    private static void MakePublishReadyViaUpdate(Event @event, Guid categoryId, Guid actingUserId)
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
                    Date = start,
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

    private static Group SeedGroupWithMember(
        string databaseName,
        User owner,
        User member,
        GroupMemberRole memberRole)
    {
        var createResult = GroupService.Create(
            "Host Org",
            "",
            GroupJoinPolicy.Open,
            owner.Id,
            DefaultGroupImageUrl);
        var group = createResult.Value.Group;
        var membership = GroupMembership.Create(group.Id, member.Id, memberRole);

        using var seedContext = CreateContext(databaseName);
        seedContext.Users.AddRange(owner, member);
        seedContext.Groups.Add(group);
        seedContext.GroupMemberships.Add(createResult.Value.OwnerMembership);
        if (member.Id != owner.Id)
        {
            seedContext.GroupMemberships.Add(membership);
        }

        seedContext.SaveChanges();
        return group;
    }

    private static User CreateUser(string subject, string email) =>
        UserService.ProvisionFromExternalIdentity(
            subject,
            email,
            emailVerified: true,
            profilePictureUrl: null,
            DefaultAvatarUrl,
            ServiceRole.User);

    private static FakeUserIdentityAccessor CreateVerifiedIdentity(string externalSubjectId, string email) =>
        new()
        {
            IsAuthenticated = true,
            ExternalSubjectId = externalSubjectId,
            Email = email,
            EmailVerified = true,
            ServiceRole = ServiceRole.User
        };

    private static UpdateEventCommandHandler CreateHandler(
        ApplicationDbContext context,
        IUserIdentityAccessor identity)
    {
        var currentUserService = new CurrentUserService(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));

        return new UpdateEventCommandHandler(
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
