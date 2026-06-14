using Application.Abstractions.Authentication;
using Application.Groups.ChangeMemberRole;
using Application.Groups.ListGroupMembers;
using Application.Groups.RemoveGroupMember;
using Application.Groups.Services;
using Application.Groups.TransferOwnership;
using Application.Users;
using Application.Users.Services;
using Domain.Groups;
using Domain.Groups.Services;
using Domain.Users;
using Domain.Users.Services;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApplicationTests.Groups.Members;

public class GroupMemberCommandHandlerTests
{
    private const string DefaultAvatarUrl = "/images/default-avatar.png";
    private const string DefaultGroupImageUrl = "/images/default-group.png";

    [Fact]
    public async Task ListGroupMembersQueryHandler_Should_ReturnMembers_When_ActorIsMember()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|list-owner", "list-owner@example.com");
        owner.Username = "owner-user";
        var member = CreateUser("auth0|list-member", "list-member@example.com");
        member.Username = "member-user";
        var group = SeedGroupWithMembers(databaseName, owner, [member]);
        var identity = CreateVerifiedIdentity("auth0|list-member", "list-member@example.com");

        await using var context = CreateContext(databaseName);
        var handler = new ListGroupMembersQueryHandler(
            context,
            CreateCurrentUserService(context, identity),
            identity,
            new GroupAccessService(context));

        // Act
        var result = await handler.Handle(new ListGroupMembersQuery(group.Id), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.Equal(GroupMemberRole.Owner, result.Value[0].Role);
        Assert.Equal("owner-user", result.Value[0].Username);
    }

    [Fact]
    public async Task ListGroupMembersQueryHandler_Should_ReturnNotMember_When_ActorIsNotMember()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|list-owner2", "list-owner2@example.com");
        var outsider = CreateUser("auth0|outsider", "outsider@example.com");
        var group = SeedGroupWithMembers(databaseName, owner, []);
        var identity = CreateVerifiedIdentity("auth0|outsider", "outsider@example.com");

        await using var context = CreateContext(databaseName);
        var handler = new ListGroupMembersQueryHandler(
            context,
            CreateCurrentUserService(context, identity),
            identity,
            new GroupAccessService(context));

        // Act
        var result = await handler.Handle(new ListGroupMembersQuery(group.Id), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Groups.NotMember", result.Error.Code);
    }

    [Fact]
    public async Task ChangeMemberRoleCommandHandler_Should_ReturnCannotDemoteSelf_When_ActorDemotesSelf()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|demote-owner", "demote-owner@example.com");
        var group = SeedGroupWithMembers(databaseName, owner, []);
        var identity = CreateVerifiedIdentity("auth0|demote-owner", "demote-owner@example.com");

        await using var context = CreateContext(databaseName);
        var handler = new ChangeMemberRoleCommandHandler(
            context,
            CreateCurrentUserService(context, identity),
            identity,
            new GroupAccessService(context));

        var command = new ChangeMemberRoleCommand(group.Id, owner.Id, GroupMemberRole.Member);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Groups.CannotDemoteSelf", result.Error.Code);
    }

    [Fact]
    public async Task ChangeMemberRoleCommandHandler_Should_UpdateRole_When_ActorCanAssignRole()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|role-owner", "role-owner@example.com");
        var member = CreateUser("auth0|role-member", "role-member@example.com");
        var group = SeedGroupWithMembers(databaseName, owner, [member]);
        var identity = CreateVerifiedIdentity("auth0|role-owner", "role-owner@example.com");

        await using var context = CreateContext(databaseName);
        var handler = new ChangeMemberRoleCommandHandler(
            context,
            CreateCurrentUserService(context, identity),
            identity,
            new GroupAccessService(context));

        var command = new ChangeMemberRoleCommand(group.Id, member.Id, GroupMemberRole.Organizer);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);

        await using var verifyContext = CreateContext(databaseName);
        var membership = await verifyContext.GroupMemberships.SingleAsync(m => m.UserId == member.Id);
        Assert.Equal(GroupMemberRole.Organizer, membership.Role);
    }

    [Fact]
    public async Task RemoveGroupMemberCommandHandler_Should_RemoveMember_When_UserLeaves()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|leave-owner", "leave-owner@example.com");
        var member = CreateUser("auth0|leave-member", "leave-member@example.com");
        var group = SeedGroupWithMembers(databaseName, owner, [member]);
        var identity = CreateVerifiedIdentity("auth0|leave-member", "leave-member@example.com");

        await using var context = CreateContext(databaseName);
        var handler = new RemoveGroupMemberCommandHandler(
            context,
            CreateCurrentUserService(context, identity),
            identity,
            new GroupAccessService(context));

        // Act
        var result = await handler.Handle(
            new RemoveGroupMemberCommand(group.Id, member.Id),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);

        await using var verifyContext = CreateContext(databaseName);
        Assert.Single(await verifyContext.GroupMemberships.ToListAsync());
    }

    [Fact]
    public async Task TransferOwnershipCommandHandler_Should_TransferOwnership_When_ActorIsOwner()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|transfer-owner", "transfer-owner@example.com");
        var member = CreateUser("auth0|transfer-member", "transfer-member@example.com");
        var group = SeedGroupWithMembers(databaseName, owner, [member]);
        var identity = CreateVerifiedIdentity("auth0|transfer-owner", "transfer-owner@example.com");

        await using var context = CreateContext(databaseName);
        var handler = new TransferOwnershipCommandHandler(
            context,
            CreateCurrentUserService(context, identity),
            identity,
            new GroupAccessService(context));

        // Act
        var result = await handler.Handle(
            new TransferOwnershipCommand(group.Id, member.Id),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);

        await using var verifyContext = CreateContext(databaseName);
        var memberships = await verifyContext.GroupMemberships.ToListAsync();
        Assert.Equal(GroupMemberRole.Owner, memberships.Single(m => m.UserId == member.Id).Role);
        Assert.Equal(GroupMemberRole.Administrator, memberships.Single(m => m.UserId == owner.Id).Role);
    }

    private static User CreateUser(string subject, string email) =>
        UserService.ProvisionFromExternalIdentity(
            subject,
            email,
            emailVerified: true,
            profilePictureUrl: null,
            DefaultAvatarUrl,
            ServiceRole.User);

    private static Group SeedGroupWithMembers(string databaseName, User owner, IReadOnlyList<User> members)
    {
        var createResult = GroupService.Create(
            "Member Org",
            "",
            GroupJoinPolicy.Open,
            owner.Id,
            DefaultGroupImageUrl);
        var group = createResult.Value.Group;
        var ownerMembership = createResult.Value.OwnerMembership;

        using var seedContext = CreateContext(databaseName);
        seedContext.Users.Add(owner);
        seedContext.Groups.Add(group);
        seedContext.GroupMemberships.Add(ownerMembership);

        foreach (var member in members)
        {
            seedContext.Users.Add(member);
            seedContext.GroupMemberships.Add(
                GroupMembership.Create(group.Id, member.Id, GroupMemberRole.Member));
        }

        seedContext.SaveChanges();
        return group;
    }

    private static CurrentUserService CreateCurrentUserService(
        ApplicationDbContext context,
        IUserIdentityAccessor identity) =>
        new(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));

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
