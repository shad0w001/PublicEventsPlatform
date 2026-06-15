using Application.Abstractions.Authentication;
using Application.Events.GetEvent;
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

namespace ApplicationTests.Events.GetEvent;

public class GetEventQueryHandlerTests
{
    private const string DefaultAvatarUrl = "/images/default-avatar.png";
    private const string DefaultGroupImageUrl = "/images/default-group.png";

    [Fact]
    public async Task GetEventQueryHandler_Should_ReturnPublicOnly_When_AnonymousGetsPublishedEvent()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|pub-view", "viewer-host@example.com");
        var eventId = await SeedPublishedEventAsync(databaseName, identity, username: "eventhost");
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, new FakeUserIdentityAccessor { IsAuthenticated = false });

        // Act
        var result = await handler.Handle(new GetEventQuery(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.Public);
        Assert.Null(result.Value.EditDetail);
        Assert.False(result.Value.CanEdit);
        Assert.Equal(EventStatus.Published, result.Value.Public!.Status);
        Assert.Equal("eventhost", result.Value.Public.HostDisplayName);
        Assert.False(result.Value.Public.HostIsGroup);
        Assert.Equal("Music", result.Value.Public.CategoryName);
    }

    [Fact]
    public async Task GetEventQueryHandler_Should_ReturnCancelledStatus_When_AnonymousGetsCancelledEvent()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|cancel-view", "cancel-host@example.com");
        var eventId = await SeedPublishedEventAsync(databaseName, identity, cancelled: true);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, new FakeUserIdentityAccessor { IsAuthenticated = false });

        // Act
        var result = await handler.Handle(new GetEventQuery(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.Public);
        Assert.Equal(EventStatus.Cancelled, result.Value.Public!.Status);
        Assert.Null(result.Value.EditDetail);
        Assert.False(result.Value.CanEdit);
    }

    [Fact]
    public async Task GetEventQueryHandler_Should_ReturnNotFound_When_AnonymousGetsDraft()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|draft-anon", "draft@example.com");
        var eventId = await SeedDraftEventAsync(databaseName, identity);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, new FakeUserIdentityAccessor { IsAuthenticated = false });

        // Act
        var result = await handler.Handle(new GetEventQuery(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task GetEventQueryHandler_Should_ReturnNotFound_When_NonEditorGetsDraft()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var ownerIdentity = CreateVerifiedIdentity("auth0|draft-owner", "owner@example.com");
        var eventId = await SeedDraftEventAsync(databaseName, ownerIdentity);

        var strangerIdentity = CreateVerifiedIdentity("auth0|draft-stranger", "stranger@example.com");
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, strangerIdentity);

        // Act
        var result = await handler.Handle(new GetEventQuery(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task GetEventQueryHandler_Should_ReturnEditDetailOnly_When_EditorGetsDraft()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|draft-editor", "editor@example.com");
        var eventId = await SeedDraftEventAsync(databaseName, identity);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new GetEventQuery(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.Public);
        Assert.NotNull(result.Value.EditDetail);
        Assert.True(result.Value.CanEdit);
        Assert.Equal(EventStatus.Draft, result.Value.EditDetail!.Status);
        Assert.Equal(eventId, result.Value.EditDetail.Id);
    }

    [Fact]
    public async Task GetEventQueryHandler_Should_ReturnEditDetailOnly_When_EditorGetsPublishedEvent()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|pub-editor", "pub-editor@example.com");
        var eventId = await SeedPublishedEventAsync(databaseName, identity);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new GetEventQuery(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.Public);
        Assert.NotNull(result.Value.EditDetail);
        Assert.True(result.Value.CanEdit);
        Assert.Equal(eventId, result.Value.EditDetail!.Id);
        Assert.Equal(EventStatus.Published, result.Value.EditDetail.Status);
        Assert.Equal("Music", result.Value.EditDetail.CategoryName);
    }

    [Fact]
    public async Task GetEventQueryHandler_Should_ReturnPublicOnly_When_NonEditorGetsPublishedEvent()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var ownerIdentity = CreateVerifiedIdentity("auth0|pub-owner", "owner@example.com");
        var eventId = await SeedPublishedEventAsync(databaseName, ownerIdentity);

        var strangerIdentity = CreateVerifiedIdentity("auth0|pub-stranger", "stranger@example.com");
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, strangerIdentity);

        // Act
        var result = await handler.Handle(new GetEventQuery(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.Public);
        Assert.Null(result.Value.EditDetail);
        Assert.False(result.Value.CanEdit);
    }

    [Fact]
    public async Task GetEventQueryHandler_Should_ReturnEditDetailWithCanEditFalse_When_EditorGetsCancelledEvent()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|cancel-editor", "cancel-editor@example.com");
        var eventId = await SeedPublishedEventAsync(databaseName, identity, cancelled: true);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new GetEventQuery(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.Public);
        Assert.NotNull(result.Value.EditDetail);
        Assert.False(result.Value.CanEdit);
        Assert.Equal(EventStatus.Cancelled, result.Value.EditDetail!.Status);
        Assert.Equal(eventId, result.Value.EditDetail.Id);
    }

    [Fact]
    public async Task GetEventQueryHandler_Should_ReturnPublicOnly_When_FormerGroupOrganizerGetsPublishedEvent()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|former-get-owner", "fget-owner@example.com");
        var formerOrganizer = CreateUser("auth0|former-get-org", "fget-org@example.com");
        var group = SeedGroupWithMember(databaseName, owner, formerOrganizer, GroupMemberRole.Organizer);

        var formerOrganizerIdentity = CreateVerifiedIdentity("auth0|former-get-org", "fget-org@example.com");
        var eventId = await SeedPublishedEventForHostAsync(
            databaseName,
            formerOrganizerIdentity,
            group.Id,
            "Former Org Event");

        await using (var seedContext = CreateContext(databaseName))
        {
            seedContext.GroupMemberships.RemoveRange(
                seedContext.GroupMemberships.Where(m => m.UserId == formerOrganizer.Id && m.GroupId == group.Id));
            await seedContext.SaveChangesAsync(CancellationToken.None);
        }

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, formerOrganizerIdentity);

        // Act
        var result = await handler.Handle(new GetEventQuery(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.Public);
        Assert.Null(result.Value.EditDetail);
        Assert.False(result.Value.CanEdit);
        Assert.Equal("Host Org", result.Value.Public!.HostDisplayName);
    }

    [Fact]
    public async Task GetEventQueryHandler_Should_ReturnNotFound_When_EventIsSoftDeleted()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|deleted-get", "deleted@example.com");
        var eventId = await SeedPublishedEventAsync(databaseName, identity, softDeleted: true);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new GetEventQuery(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.Deleted", result.Error.Code);
    }

    [Fact]
    public async Task GetEventQueryHandler_Should_ReturnGroupHostDisplayName_When_EventHostedByGroup()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|group-host-owner", "gowner@example.com");
        var organizer = CreateUser("auth0|group-host-org", "gorg@example.com");
        var group = SeedGroupWithMember(databaseName, owner, organizer, GroupMemberRole.Organizer);

        var organizerIdentity = CreateVerifiedIdentity("auth0|group-host-org", "gorg@example.com");
        var eventId = await SeedPublishedEventForHostAsync(
            databaseName,
            organizerIdentity,
            group.Id,
            "Group Event");

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, new FakeUserIdentityAccessor { IsAuthenticated = false });

        // Act
        var result = await handler.Handle(new GetEventQuery(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.Public);
        Assert.Equal("Host Org", result.Value.Public!.HostDisplayName);
        Assert.True(result.Value.Public.HostIsGroup);
    }

    private static async Task<Guid> SeedDraftEventAsync(
        string databaseName,
        FakeUserIdentityAccessor identity)
    {
        await using var context = CreateContext(databaseName);
        var currentUserService = new CurrentUserService(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));
        var user = (await currentUserService.GetOrProvisionAsync(CancellationToken.None)).Value;

        var createResult = EventService.Create(EventTier.Small, "Draft Event", user.Id);
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
        string? username = null,
        bool cancelled = false,
        bool softDeleted = false)
    {
        await using var context = CreateContext(databaseName);
        var currentUserService = new CurrentUserService(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));
        var user = (await currentUserService.GetOrProvisionAsync(CancellationToken.None)).Value;

        if (username is not null)
        {
            user.Username = username;
        }

        var createResult = EventService.Create(EventTier.Small, "Published Event", user.Id);
        var @event = createResult.Value.Event;
        var organizer = createResult.Value.Organizer;

        var categoryId = SeedCategoryInContext(context);
        MakePublishReadyViaUpdate(@event, categoryId, user.Id);

        if (softDeleted)
        {
            EventService.SoftDelete(@event);
        }
        else
        {
            EventService.Publish(@event, categoryExists: true, recentPublishCount: 0, maxPublishesPerWeek: 100);

            if (cancelled)
            {
                EventService.Cancel(@event);
            }
        }

        context.Events.Add(@event);
        context.EventOrganizers.Add(organizer);
        await context.SaveChangesAsync(CancellationToken.None);
        return @event.Id;
    }

    private static async Task<Guid> SeedPublishedEventForHostAsync(
        string databaseName,
        FakeUserIdentityAccessor identity,
        Guid hostParticipantId,
        string title)
    {
        await using var context = CreateContext(databaseName);
        var currentUserService = new CurrentUserService(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));
        var user = (await currentUserService.GetOrProvisionAsync(CancellationToken.None)).Value;

        var createResult = EventService.Create(EventTier.Small, title, hostParticipantId);
        var @event = createResult.Value.Event;
        var organizer = createResult.Value.Organizer;

        var categoryId = SeedCategoryInContext(context);
        MakePublishReadyViaUpdate(@event, categoryId, user.Id);

        EventService.Publish(@event, categoryExists: true, recentPublishCount: 0, maxPublishesPerWeek: 100);

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

    private static GetEventQueryHandler CreateHandler(
        ApplicationDbContext context,
        IUserIdentityAccessor identity)
    {
        var currentUserService = new CurrentUserService(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));

        return new GetEventQueryHandler(
            context,
            identity,
            currentUserService,
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
