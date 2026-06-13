using Application.Abstractions.Authentication;
using Application.Groups;
using Application.Groups.CreateGroup;
using Application.Users;
using Application.Users.Services;
using Domain.Groups;
using Domain.Users;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApplicationTests.Groups.CreateGroup;

public class CreateGroupCommandHandlerTests
{
    private const string DefaultAvatarUrl = "/images/default-avatar.png";
    private const string DefaultGroupImageUrl = "/images/default-group.png";

    [Fact]
    public async Task CreateGroupCommandHandler_Should_CreateGroupWithOwnerMembership_When_UserIsVerified()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|create-group-subject", "creator@example.com");

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        var command = new CreateGroupCommand(
            "Test Org",
            "A test organization",
            GroupJoinPolicy.ApplicationRequired,
            ProfileImageUrl: "https://example.com/group.png");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Test Org", result.Value.Name);
        Assert.Equal("A test organization", result.Value.Description);
        Assert.Equal(GroupJoinPolicy.ApplicationRequired, result.Value.JoinPolicy);
        Assert.Equal("https://example.com/group.png", result.Value.ProfileImageUrl);

        await using var verifyContext = CreateContext(databaseName);
        var persistedGroup = await verifyContext.Groups.SingleAsync();
        var persistedMembership = await verifyContext.GroupMemberships.SingleAsync();
        Assert.Equal("Test Org", persistedGroup.Name);
        Assert.Equal(GroupMemberRole.Owner, persistedMembership.Role);
    }

    [Fact]
    public async Task CreateGroupCommandHandler_Should_ReturnUnauthorized_When_NotAuthenticated()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = new FakeUserIdentityAccessor { IsAuthenticated = false };

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        var command = new CreateGroupCommand("Test Org", null, GroupJoinPolicy.Open, null);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Users.Unauthorized", result.Error.Code);
    }

    [Fact]
    public async Task CreateGroupCommandHandler_Should_ReturnEmailNotVerified_When_EmailIsUnverified()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = new FakeUserIdentityAccessor
        {
            IsAuthenticated = true,
            ExternalSubjectId = "auth0|unverified-subject",
            Email = "unverified@example.com",
            EmailVerified = false,
            ServiceRole = ServiceRole.User
        };

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        var command = new CreateGroupCommand("Test Org", null, GroupJoinPolicy.Open, null);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Users.EmailNotVerified", result.Error.Code);
    }

    [Fact]
    public async Task CreateGroupCommandHandler_Should_ReturnInvalidName_When_NameIsEmpty()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|invalid-name-subject", "user@example.com");

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        var command = new CreateGroupCommand("   ", null, GroupJoinPolicy.Open, null);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Groups.InvalidName", result.Error.Code);
    }

    [Fact]
    public async Task CreateGroupCommandHandler_Should_ReturnNameTooLong_When_NameExceedsMaxLength()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|long-name-subject", "user@example.com");
        var name = new string('a', GroupConstants.NameMaxLength + 1);

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        var command = new CreateGroupCommand(name, null, GroupJoinPolicy.Open, null);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Groups.NameTooLong", result.Error.Code);
    }

    [Fact]
    public async Task CreateGroupCommandHandler_Should_UseDefaultGroupImage_When_ProfileImageUrlIsNull()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|default-image-subject", "user@example.com");

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        var command = new CreateGroupCommand("Test Org", null, GroupJoinPolicy.Open, ProfileImageUrl: null);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(DefaultGroupImageUrl, result.Value.ProfileImageUrl);

        await using var verifyContext = CreateContext(databaseName);
        var persistedGroup = await verifyContext.Groups.SingleAsync();
        Assert.Equal(DefaultGroupImageUrl, persistedGroup.ProfileImageUrl);
    }

    private static FakeUserIdentityAccessor CreateVerifiedIdentity(
        string externalSubjectId,
        string email) =>
        new()
        {
            IsAuthenticated = true,
            ExternalSubjectId = externalSubjectId,
            Email = email,
            EmailVerified = true,
            ServiceRole = ServiceRole.User
        };

    private static CreateGroupCommandHandler CreateHandler(
        ApplicationDbContext context,
        IUserIdentityAccessor identity)
    {
        var currentUserService = new CurrentUserService(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));

        return new CreateGroupCommandHandler(
            context,
            currentUserService,
            identity,
            Options.Create(new GroupProfileOptions { DefaultImageUrl = DefaultGroupImageUrl }));
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
