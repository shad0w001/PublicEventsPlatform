using Application.Abstractions.Authentication;
using Application.Groups.Services;
using Application.Groups.UpdateGroup;
using Application.Users;
using Application.Users.Services;
using Domain.Groups;
using Domain.Groups.Services;
using Domain.Users;
using Domain.Users.Services;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApplicationTests.Groups.UpdateGroup;

public class UpdateGroupCommandHandlerTests
{
    private const string DefaultAvatarUrl = "/images/default-avatar.png";
    private const string DefaultGroupImageUrl = "/images/default-group.png";

    [Fact]
    public async Task UpdateGroupCommandHandler_Should_UpdateProfile_When_ActorIsAdministrator()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|owner", "owner@example.com");
        var admin = CreateUser("auth0|admin", "admin@example.com");
        var group = SeedGroup(databaseName, owner, admin, GroupMemberRole.Administrator);
        var identity = CreateVerifiedIdentity("auth0|admin", "admin@example.com");

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        var command = new UpdateGroupCommand(group.Id, Name: "Updated Org");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Updated Org", result.Value.Name);

        await using var verifyContext = CreateContext(databaseName);
        var persisted = await verifyContext.Groups.SingleAsync();
        Assert.Equal("Updated Org", persisted.Name);
    }

    [Fact]
    public async Task UpdateGroupCommandHandler_Should_ReturnInsufficientPermissions_When_ActorIsMember()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|owner2", "owner2@example.com");
        var member = CreateUser("auth0|member", "member@example.com");
        var group = SeedGroup(databaseName, owner, member, GroupMemberRole.Member);
        var identity = CreateVerifiedIdentity("auth0|member", "member@example.com");

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        var command = new UpdateGroupCommand(group.Id, Name: "Updated Org");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Groups.InsufficientPermissions", result.Error.Code);
    }

    [Fact]
    public async Task UpdateGroupCommandHandler_Should_ClearApplications_When_JoinPolicyChanges()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|owner3", "owner3@example.com");
        var applicant = CreateUser("auth0|applicant", "applicant@example.com");
        var group = SeedGroup(databaseName, owner, owner, GroupMemberRole.Owner);
        var application = GroupService.SubmitJoinApplication(
            group,
            applicant.Id,
            DateTime.UtcNow).Value;

        await using (var seedContext = CreateContext(databaseName))
        {
            seedContext.Users.Add(applicant);
            seedContext.GroupJoinApplications.Add(application);
            await seedContext.SaveChangesAsync();
        }

        var identity = CreateVerifiedIdentity("auth0|owner3", "owner3@example.com");

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        var command = new UpdateGroupCommand(group.Id, JoinPolicy: GroupJoinPolicy.Open);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(GroupJoinPolicy.Open, result.Value.JoinPolicy);

        await using var verifyContext = CreateContext(databaseName);
        Assert.Empty(await verifyContext.GroupJoinApplications.ToListAsync());
    }

    private static User CreateUser(string subject, string email) =>
        UserService.ProvisionFromExternalIdentity(
            subject,
            email,
            emailVerified: true,
            profilePictureUrl: null,
            DefaultAvatarUrl,
            ServiceRole.User);

    private static Group SeedGroup(
        string databaseName,
        User owner,
        User actor,
        GroupMemberRole actorRole)
    {
        var createResult = GroupService.Create(
            "Test Org",
            "Description",
            GroupJoinPolicy.ApplicationRequired,
            owner.Id,
            DefaultGroupImageUrl);
        var group = createResult.Value.Group;
        var ownerMembership = createResult.Value.OwnerMembership;

        GroupMembership? actorMembership = null;
        if (actor.Id != owner.Id)
        {
            actorMembership = GroupMembership.Create(group.Id, actor.Id, actorRole);
        }
        else if (actorRole != GroupMemberRole.Owner)
        {
            ownerMembership.Role = actorRole;
        }

        using var seedContext = CreateContext(databaseName);
        seedContext.Users.Add(owner);
        if (actor.Id != owner.Id)
        {
            seedContext.Users.Add(actor);
        }

        seedContext.Groups.Add(group);
        seedContext.GroupMemberships.Add(ownerMembership);
        if (actorMembership is not null)
        {
            seedContext.GroupMemberships.Add(actorMembership);
        }

        seedContext.SaveChanges();
        return group;
    }

    private static FakeUserIdentityAccessor CreateVerifiedIdentity(string subject, string email) =>
        new()
        {
            IsAuthenticated = true,
            ExternalSubjectId = subject,
            Email = email,
            EmailVerified = true,
            ServiceRole = ServiceRole.User
        };

    private static UpdateGroupCommandHandler CreateHandler(
        ApplicationDbContext context,
        IUserIdentityAccessor identity)
    {
        var currentUserService = new CurrentUserService(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));

        return new UpdateGroupCommandHandler(
            context,
            currentUserService,
            identity,
            new GroupAccessService(context));
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
