using Application.Abstractions.Authentication;
using Application.Groups.ListMyGroups;
using Application.Users.Services;
using Domain.Groups;
using Domain.Groups.Services;
using Domain.Users;
using Domain.Users.Services;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApplicationTests.Groups.ListMyGroups;

public class ListMyGroupsQueryHandlerTests
{
    private const string DefaultAvatarUrl = "/images/default-avatar.png";
    private const string DefaultGroupImageUrl = "/images/default-group.png";
    private static readonly DateTime OlderJoin = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime NewerJoin = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ListMyGroupsQueryHandler_Should_ReturnMembershipsWithRole_When_UserBelongsToActiveGroups()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|multi-owner", "owner@example.com");
        var member = CreateUser("auth0|multi-member", "member@example.com");

        var ownedResult = GroupService.Create("Owned Org", "Desc", GroupJoinPolicy.Open, member.Id, DefaultGroupImageUrl);
        var memberResult = GroupService.Create("Member Org", "", GroupJoinPolicy.Open, owner.Id, DefaultGroupImageUrl);
        var memberMembership = GroupMembership.Create(memberResult.Value.Group.Id, member.Id, GroupMemberRole.Member);
        memberMembership.JoinedAt = NewerJoin;

        await using (var seedContext = CreateContext(databaseName))
        {
            seedContext.Users.AddRange(owner, member);
            seedContext.Groups.AddRange(ownedResult.Value.Group, memberResult.Value.Group);
            seedContext.GroupMemberships.AddRange(
                ownedResult.Value.OwnerMembership,
                memberResult.Value.OwnerMembership,
                memberMembership);
            await seedContext.SaveChangesAsync();
        }

        var identity = CreateVerifiedIdentity("auth0|multi-member", "member@example.com");
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new ListMyGroupsQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);

        var owned = result.Value.Single(g => g.Name == "Owned Org");
        Assert.Equal(GroupMemberRole.Owner, owned.MyRole);
        Assert.Equal(1, owned.MemberCount);

        var memberOrg = result.Value.Single(g => g.Name == "Member Org");
        Assert.Equal(GroupMemberRole.Member, memberOrg.MyRole);
        Assert.Equal(2, memberOrg.MemberCount);
    }

    [Fact]
    public async Task ListMyGroupsQueryHandler_Should_ExcludeDeletedGroups_When_MembershipExistsOnSoftDeletedOrg()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|deleted-list-owner", "deleted@example.com");

        var activeResult = GroupService.Create("Active Org", "", GroupJoinPolicy.Open, owner.Id, DefaultGroupImageUrl);
        var deletedResult = GroupService.Create("Deleted Org", "", GroupJoinPolicy.Open, owner.Id, DefaultGroupImageUrl);
        GroupService.SoftDelete(deletedResult.Value.Group);

        await using (var seedContext = CreateContext(databaseName))
        {
            seedContext.Users.Add(owner);
            seedContext.Groups.AddRange(activeResult.Value.Group, deletedResult.Value.Group);
            seedContext.GroupMemberships.AddRange(
                activeResult.Value.OwnerMembership,
                deletedResult.Value.OwnerMembership);
            await seedContext.SaveChangesAsync();
        }

        var identity = CreateVerifiedIdentity("auth0|deleted-list-owner", "deleted@example.com");
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new ListMyGroupsQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal("Active Org", result.Value[0].Name);
    }

    [Fact]
    public async Task ListMyGroupsQueryHandler_Should_ReturnEmptyList_When_UserHasNoMemberships()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var user = CreateUser("auth0|no-groups", "nogroups@example.com");

        await using (var seedContext = CreateContext(databaseName))
        {
            seedContext.Users.Add(user);
            await seedContext.SaveChangesAsync();
        }

        var identity = CreateVerifiedIdentity("auth0|no-groups", "nogroups@example.com");
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new ListMyGroupsQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task ListMyGroupsQueryHandler_Should_ReturnForbidden_When_EmailNotVerified()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        await using var context = CreateContext(databaseName);
        var identity = new FakeUserIdentityAccessor
        {
            IsAuthenticated = true,
            ExternalSubjectId = "auth0|unverified",
            Email = "unverified@example.com",
            EmailVerified = false,
            ServiceRole = ServiceRole.User
        };
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new ListMyGroupsQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Users.EmailNotVerified", result.Error.Code);
    }

    [Fact]
    public async Task ListMyGroupsQueryHandler_Should_OrderByRoleRankThenJoinedAt_When_MultipleMemberships()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var user = CreateUser("auth0|sort-user", "sort@example.com");
        var otherOwner = CreateUser("auth0|sort-other", "other@example.com");

        var ownerResult = GroupService.Create("Owner Org", "", GroupJoinPolicy.Open, user.Id, DefaultGroupImageUrl);
        var olderMemberResult = GroupService.Create("Older Member Org", "", GroupJoinPolicy.Open, otherOwner.Id, DefaultGroupImageUrl);
        var newerMemberResult = GroupService.Create("Newer Member Org", "", GroupJoinPolicy.Open, otherOwner.Id, DefaultGroupImageUrl);

        var ownerMembership = ownerResult.Value.OwnerMembership;
        ownerMembership.JoinedAt = OlderJoin;

        var olderMemberMembership = GroupMembership.Create(
            olderMemberResult.Value.Group.Id,
            user.Id,
            GroupMemberRole.Member);
        olderMemberMembership.JoinedAt = OlderJoin;

        var newerMemberMembership = GroupMembership.Create(
            newerMemberResult.Value.Group.Id,
            user.Id,
            GroupMemberRole.Member);
        newerMemberMembership.JoinedAt = NewerJoin;

        await using (var seedContext = CreateContext(databaseName))
        {
            seedContext.Users.AddRange(user, otherOwner);
            seedContext.Groups.AddRange(
                ownerResult.Value.Group,
                olderMemberResult.Value.Group,
                newerMemberResult.Value.Group);
            seedContext.GroupMemberships.AddRange(
                ownerMembership,
                olderMemberResult.Value.OwnerMembership,
                newerMemberResult.Value.OwnerMembership,
                olderMemberMembership,
                newerMemberMembership);
            await seedContext.SaveChangesAsync();
        }

        var identity = CreateVerifiedIdentity("auth0|sort-user", "sort@example.com");
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new ListMyGroupsQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Count);
        Assert.Equal("Owner Org", result.Value[0].Name);
        Assert.Equal(GroupMemberRole.Owner, result.Value[0].MyRole);
        Assert.Equal("Newer Member Org", result.Value[1].Name);
        Assert.Equal("Older Member Org", result.Value[2].Name);
    }

    private static User CreateUser(string subject, string email) =>
        UserService.ProvisionFromExternalIdentity(
            subject,
            email,
            emailVerified: true,
            profilePictureUrl: null,
            DefaultAvatarUrl,
            ServiceRole.User);

    private static FakeUserIdentityAccessor CreateVerifiedIdentity(string subject, string email) =>
        new()
        {
            IsAuthenticated = true,
            ExternalSubjectId = subject,
            Email = email,
            EmailVerified = true,
            ServiceRole = ServiceRole.User
        };

    private static ListMyGroupsQueryHandler CreateHandler(
        ApplicationDbContext context,
        IUserIdentityAccessor identity) =>
        new(
            context,
            new CurrentUserService(
                context,
                identity,
                Options.Create(new Application.Users.UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl })),
            identity);

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
