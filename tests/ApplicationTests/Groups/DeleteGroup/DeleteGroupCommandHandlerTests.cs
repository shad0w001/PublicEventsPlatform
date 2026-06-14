using Application.Abstractions.Authentication;
using Application.Groups.DeleteGroup;
using Application.Groups.Services;
using Application.Users;
using Application.Users.Services;
using Domain.Groups;
using Domain.Groups.Services;
using Domain.Users;
using Domain.Users.Services;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApplicationTests.Groups.DeleteGroup;

public class DeleteGroupCommandHandlerTests
{
    private const string DefaultAvatarUrl = "/images/default-avatar.png";
    private const string DefaultGroupImageUrl = "/images/default-group.png";
    private static readonly DateTime UtcNow = new(2026, 6, 13, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task DeleteGroupCommandHandler_Should_SoftDeleteGroup_When_CallerIsOwner()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|delete-owner", "delete-owner@example.com");
        var applicant = CreateUser("auth0|delete-applicant", "applicant@example.com");

        var createResult = GroupService.Create(
            "Delete Me Org",
            "",
            GroupJoinPolicy.ApplicationRequired,
            owner.Id,
            DefaultGroupImageUrl);
        var group = createResult.Value.Group;
        var application = GroupService.SubmitJoinApplication(group, applicant.Id, UtcNow).Value;

        await using (var seedContext = CreateContext(databaseName))
        {
            seedContext.Users.AddRange(owner, applicant);
            seedContext.Groups.Add(group);
            seedContext.GroupMemberships.Add(createResult.Value.OwnerMembership);
            seedContext.GroupJoinApplications.Add(application);
            await seedContext.SaveChangesAsync();
        }

        var identity = CreateVerifiedIdentity("auth0|delete-owner", "delete-owner@example.com");
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new DeleteGroupCommand(group.Id), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);

        await using var verifyContext = CreateContext(databaseName);
        var deletedGroup = await verifyContext.Groups.SingleAsync(g => g.Id == group.Id);
        Assert.NotNull(deletedGroup.DeletedAt);
        Assert.Empty(await verifyContext.GroupJoinApplications.Where(a => a.GroupId == group.Id).ToListAsync());
        Assert.Single(await verifyContext.GroupMemberships.Where(m => m.GroupId == group.Id).ToListAsync());
    }

    [Fact]
    public async Task DeleteGroupCommandHandler_Should_ReturnInsufficientPermissions_When_CallerIsNotOwner()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|delete-admin-owner", "admin-owner@example.com");
        var admin = CreateUser("auth0|delete-admin", "admin@example.com");

        var createResult = GroupService.Create(
            "Protected Org",
            "",
            GroupJoinPolicy.Open,
            owner.Id,
            DefaultGroupImageUrl);
        var group = createResult.Value.Group;

        await using (var seedContext = CreateContext(databaseName))
        {
            seedContext.Users.AddRange(owner, admin);
            seedContext.Groups.Add(group);
            seedContext.GroupMemberships.AddRange(
                createResult.Value.OwnerMembership,
                GroupMembership.Create(group.Id, admin.Id, GroupMemberRole.Administrator));
            await seedContext.SaveChangesAsync();
        }

        var identity = CreateVerifiedIdentity("auth0|delete-admin", "admin@example.com");
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new DeleteGroupCommand(group.Id), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Groups.InsufficientPermissions", result.Error.Code);
    }

    [Fact]
    public async Task DeleteGroupCommandHandler_Should_ReturnInsufficientPermissions_When_CallerIsNotMember()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|delete-outsider-owner", "outsider-owner@example.com");
        var outsider = CreateUser("auth0|delete-outsider", "outsider@example.com");

        var createResult = GroupService.Create(
            "Outsider Org",
            "",
            GroupJoinPolicy.Open,
            owner.Id,
            DefaultGroupImageUrl);
        var group = createResult.Value.Group;

        await using (var seedContext = CreateContext(databaseName))
        {
            seedContext.Users.AddRange(owner, outsider);
            seedContext.Groups.Add(group);
            seedContext.GroupMemberships.Add(createResult.Value.OwnerMembership);
            await seedContext.SaveChangesAsync();
        }

        var identity = CreateVerifiedIdentity("auth0|delete-outsider", "outsider@example.com");
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new DeleteGroupCommand(group.Id), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Groups.InsufficientPermissions", result.Error.Code);
    }

    [Fact]
    public async Task DeleteGroupCommandHandler_Should_ReturnDeleted_When_GroupAlreadySoftDeleted()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|delete-twice-owner", "twice-owner@example.com");

        var createResult = GroupService.Create(
            "Twice Org",
            "",
            GroupJoinPolicy.Open,
            owner.Id,
            DefaultGroupImageUrl);
        var group = createResult.Value.Group;
        GroupService.SoftDelete(group);

        await using (var seedContext = CreateContext(databaseName))
        {
            seedContext.Users.Add(owner);
            seedContext.Groups.Add(group);
            seedContext.GroupMemberships.Add(createResult.Value.OwnerMembership);
            await seedContext.SaveChangesAsync();
        }

        var identity = CreateVerifiedIdentity("auth0|delete-twice-owner", "twice-owner@example.com");
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new DeleteGroupCommand(group.Id), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Groups.Deleted", result.Error.Code);
    }

    [Fact]
    public async Task DeleteGroupCommandHandler_Should_ReturnForbidden_When_EmailNotVerified()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        await using var context = CreateContext(databaseName);
        var identity = new FakeUserIdentityAccessor
        {
            IsAuthenticated = true,
            ExternalSubjectId = "auth0|unverified-delete",
            Email = "unverified@example.com",
            EmailVerified = false,
            ServiceRole = ServiceRole.User
        };
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(
            new DeleteGroupCommand(Guid.NewGuid()),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Users.EmailNotVerified", result.Error.Code);
    }

    private static User CreateUser(string subject, string email) =>
        UserService.ProvisionFromExternalIdentity(
            subject,
            email,
            emailVerified: true,
            profilePictureUrl: null,
            DefaultAvatarUrl,
            ServiceRole.User);

    private static DeleteGroupCommandHandler CreateHandler(
        ApplicationDbContext context,
        IUserIdentityAccessor identity) =>
        new(
            context,
            new CurrentUserService(
                context,
                identity,
                Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl })),
            identity,
            new GroupAccessService(context));

    private static FakeUserIdentityAccessor CreateVerifiedIdentity(string subject, string email) =>
        new()
        {
            IsAuthenticated = true,
            ExternalSubjectId = subject,
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
