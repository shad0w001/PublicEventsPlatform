using Application.Abstractions.Authentication;
using Application.Groups.GetGroup;
using Domain.Groups;
using Domain.Groups.Services;
using Domain.Users;
using Domain.Users.Services;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace ApplicationTests.Groups.GetGroup;

public class GetGroupQueryHandlerTests
{
    private const string DefaultAvatarUrl = "/images/default-avatar.png";
    private const string DefaultGroupImageUrl = "/images/default-group.png";

    [Fact]
    public async Task GetGroupQueryHandler_Should_ReturnGroupWithMemberCount_When_GroupExists()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|owner-subject", "owner@example.com");
        var member = CreateUser("auth0|member-subject", "member@example.com");
        var group = SeedGroup(databaseName, owner, member);

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, new FakeUserIdentityAccessor { IsAuthenticated = false });

        // Act
        var result = await handler.Handle(new GetGroupQuery(group.Id), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Public Org", result.Value.Name);
        Assert.Equal(2, result.Value.MemberCount);
        Assert.Null(result.Value.MyRole);
    }

    [Fact]
    public async Task GetGroupQueryHandler_Should_ReturnMyRole_When_AuthenticatedMember()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|owner-subject", "owner@example.com");
        var member = CreateUser("auth0|member-subject", "member@example.com");
        var group = SeedGroup(databaseName, owner, member);

        await using var context = CreateContext(databaseName);
        var identity = new FakeUserIdentityAccessor
        {
            IsAuthenticated = true,
            ExternalSubjectId = "auth0|member-subject",
            Email = "member@example.com",
            EmailVerified = true,
            ServiceRole = ServiceRole.User
        };
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new GetGroupQuery(group.Id), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(GroupMemberRole.Member, result.Value.MyRole);
    }

    [Fact]
    public async Task GetGroupQueryHandler_Should_ReturnNullMyRole_When_AuthenticatedNonMember()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|owner-subject", "owner@example.com");
        var outsider = CreateUser("auth0|outsider-subject", "outsider@example.com");
        var group = SeedGroup(databaseName, owner, null);

        await using (var seedContext = CreateContext(databaseName))
        {
            seedContext.Users.Add(outsider);
            await seedContext.SaveChangesAsync();
        }

        await using var context = CreateContext(databaseName);
        var identity = new FakeUserIdentityAccessor
        {
            IsAuthenticated = true,
            ExternalSubjectId = "auth0|outsider-subject",
            Email = "outsider@example.com",
            EmailVerified = true,
            ServiceRole = ServiceRole.User
        };
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new GetGroupQuery(group.Id), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.MyRole);
    }

    [Fact]
    public async Task GetGroupQueryHandler_Should_ReturnIsVerified_When_GroupIsVerified()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|verified-owner", "verified-owner@example.com");
        var group = SeedGroup(databaseName, owner, null);
        await using (var seedContext = CreateContext(databaseName))
        {
            var persisted = await seedContext.Groups.SingleAsync(g => g.Id == group.Id);
            persisted.IsVerified = true;
            persisted.VerifiedAt = DateTime.UtcNow;
            await seedContext.SaveChangesAsync();
        }

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, new FakeUserIdentityAccessor { IsAuthenticated = false });

        // Act
        var result = await handler.Handle(new GetGroupQuery(group.Id), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsVerified);
    }

    [Fact]
    public async Task GetGroupQueryHandler_Should_ReturnNotFound_When_GroupIsDeleted()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|deleted-owner", "deleted@example.com");

        var createResult = GroupService.Create(
            "Deleted Org",
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

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, new FakeUserIdentityAccessor { IsAuthenticated = false });

        // Act
        var result = await handler.Handle(new GetGroupQuery(group.Id), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Groups.Deleted", result.Error.Code);
    }

    private static User CreateUser(string subject, string email) =>
        UserService.ProvisionFromExternalIdentity(
            subject,
            email,
            emailVerified: true,
            profilePictureUrl: null,
            DefaultAvatarUrl,
            ServiceRole.User);

    private static Group SeedGroup(string databaseName, User owner, User? member)
    {
        var createResult = GroupService.Create(
            "Public Org",
            "Description",
            GroupJoinPolicy.Open,
            owner.Id,
            DefaultGroupImageUrl);
        var group = createResult.Value.Group;
        var ownerMembership = createResult.Value.OwnerMembership;

        using var seedContext = CreateContext(databaseName);
        seedContext.Users.Add(owner);
        seedContext.Groups.Add(group);
        seedContext.GroupMemberships.Add(ownerMembership);

        if (member is not null)
        {
            seedContext.Users.Add(member);
            seedContext.GroupMemberships.Add(
                GroupMembership.Create(group.Id, member.Id, GroupMemberRole.Member));
        }

        seedContext.SaveChanges();
        return group;
    }

    private static GetGroupQueryHandler CreateHandler(
        ApplicationDbContext context,
        IUserIdentityAccessor identity) =>
        new(
            context,
            identity,
            new Application.Users.Services.CurrentUserService(
                context,
                identity,
                Microsoft.Extensions.Options.Options.Create(
                    new Application.Users.UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl })));

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
