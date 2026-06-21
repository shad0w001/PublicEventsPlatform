using Application.Abstractions.Authentication;
using Application.Events;
using Application.Events.CreateEvent;
using Application.Events.Services;
using Application.Users;
using Application.Users.Services;
using Domain.Events;
using Domain.Groups;
using Domain.Groups.Services;
using Domain.Users;
using Domain.Users.Services;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApplicationTests.Events.CreateEvent;

public class CreateEventCommandHandlerTests
{
    private const string DefaultAvatarUrl = "/images/default-avatar.png";
    private const string DefaultGroupImageUrl = "/images/default-group.png";
    private const string DefaultBannerUrl = "/images/default-event-banner.png";

    [Fact]
    public async Task CreateEventCommandHandler_Should_CreateDraftWithSelfHost_When_HostIdIsOmitted()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|create-event-self", "creator@example.com");

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        var command = new CreateEventCommand(EventTier.Small, "My Event", HostId: null);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("My Event", result.Value.Title);
        Assert.Equal(EventTier.Small, result.Value.Tier);
        Assert.Equal(EventStatus.Draft, result.Value.Status);
        Assert.False(result.Value.HostIsGroup);

        await using var verifyContext = CreateContext(databaseName);
        var user = await verifyContext.Users.SingleAsync();
        var persistedEvent = await verifyContext.Events.SingleAsync();
        var organizer = await verifyContext.EventOrganizers.SingleAsync();
        Assert.Equal(user.Id, organizer.ParticipantId);
        Assert.Equal(user.Id, result.Value.HostParticipantId);
        Assert.Null(persistedEvent.CreatedByUserId);
        Assert.Equal(DefaultBannerUrl, persistedEvent.BannerImageUrl);
    }

    [Fact]
    public async Task CreateEventCommandHandler_Should_CreateDraftWithSelfHost_When_HostIdIsCallerParticipantId()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|create-event-explicit-self", "self@example.com");

        await using var context = CreateContext(databaseName);
        var currentUserService = new CurrentUserService(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));
        var userResult = await currentUserService.GetOrProvisionAsync(CancellationToken.None);
        var handler = CreateHandler(context, identity);

        var command = new CreateEventCommand(EventTier.Big, "Explicit Self", userResult.Value.Id);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(EventTier.Big, result.Value.Tier);
        Assert.Equal(userResult.Value.Id, result.Value.HostParticipantId);
        Assert.False(result.Value.HostIsGroup);
    }

    [Fact]
    public async Task CreateEventCommandHandler_Should_CreateDraftWithGroupHost_When_CallerIsOrganizer()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|group-owner", "owner@example.com");
        var organizer = CreateUser("auth0|group-organizer", "organizer@example.com");
        var group = SeedGroupWithMember(databaseName, owner, organizer, GroupMemberRole.Organizer);
        var identity = CreateVerifiedIdentity("auth0|group-organizer", "organizer@example.com");

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        var command = new CreateEventCommand(EventTier.Small, "Org Event", group.Id);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(group.Id, result.Value.HostParticipantId);
        Assert.True(result.Value.HostIsGroup);

        await using var verifyContext = CreateContext(databaseName);
        var organizerRow = await verifyContext.EventOrganizers.SingleAsync();
        Assert.Equal(group.Id, organizerRow.ParticipantId);
    }

    [Fact]
    public async Task CreateEventCommandHandler_Should_ReturnInsufficientHostPermissions_When_CallerIsGroupMember()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|group-owner2", "owner2@example.com");
        var member = CreateUser("auth0|group-member", "member@example.com");
        var group = SeedGroupWithMember(databaseName, owner, member, GroupMemberRole.Member);
        var identity = CreateVerifiedIdentity("auth0|group-member", "member@example.com");

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        var command = new CreateEventCommand(EventTier.Small, "Forbidden", group.Id);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.InsufficientHostPermissions", result.Error.Code);
    }

    [Fact]
    public async Task CreateEventCommandHandler_Should_ReturnHostNotFound_When_GroupIsSoftDeleted()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|deleted-group-owner", "deleted-owner@example.com");
        var group = SeedGroupWithMember(databaseName, owner, owner, GroupMemberRole.Owner);

        await using (var seedContext = CreateContext(databaseName))
        {
            var persistedGroup = await seedContext.Groups.SingleAsync();
            GroupService.SoftDelete(persistedGroup);
            await seedContext.SaveChangesAsync();
        }

        var identity = CreateVerifiedIdentity("auth0|deleted-group-owner", "deleted-owner@example.com");
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        var command = new CreateEventCommand(EventTier.Small, "Deleted Host", group.Id);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.HostNotFound", result.Error.Code);
    }

    [Fact]
    public async Task CreateEventCommandHandler_Should_ReturnHostNotFound_When_HostIdIsUnknown()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|unknown-host", "unknown@example.com");

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        var command = new CreateEventCommand(
            EventTier.Small,
            "Unknown Host",
            Guid.Parse("99999999-9999-9999-9999-999999999999"));

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.HostNotFound", result.Error.Code);
    }

    [Fact]
    public async Task CreateEventCommandHandler_Should_ReturnInsufficientHostPermissions_When_HostIdIsAnotherUser()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var otherUser = CreateUser("auth0|other-user", "other@example.com");
        var creator = CreateUser("auth0|creator-user", "creator@example.com");

        await using (var seedContext = CreateContext(databaseName))
        {
            seedContext.Users.AddRange(otherUser, creator);
            await seedContext.SaveChangesAsync();
        }

        var identity = CreateVerifiedIdentity("auth0|creator-user", "creator@example.com");
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        var command = new CreateEventCommand(EventTier.Small, "Wrong Host", otherUser.Id);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.InsufficientHostPermissions", result.Error.Code);
    }

    [Fact]
    public async Task CreateEventCommandHandler_Should_ReturnInvalidTitle_When_TitleIsEmpty()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|empty-title", "empty@example.com");

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        var command = new CreateEventCommand(EventTier.Small, "   ", null);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.InvalidTitle", result.Error.Code);
    }

    [Fact]
    public async Task CreateEventCommandHandler_Should_ReturnEmailNotVerified_When_EmailIsUnverified()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = new FakeUserIdentityAccessor
        {
            IsAuthenticated = true,
            ExternalSubjectId = "auth0|unverified",
            Email = "unverified@example.com",
            EmailVerified = false,
            ServiceRole = ServiceRole.User
        };

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        var command = new CreateEventCommand(EventTier.Small, "Unverified", null);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Users.EmailNotVerified", result.Error.Code);
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

    private static CreateEventCommandHandler CreateHandler(
        ApplicationDbContext context,
        IUserIdentityAccessor identity)
    {
        var currentUserService = new CurrentUserService(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));

        return new CreateEventCommandHandler(
            context,
            currentUserService,
            identity,
            new EventAccessService(context),
            Options.Create(new EventOptions { DefaultBannerUrl = DefaultBannerUrl }));
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
