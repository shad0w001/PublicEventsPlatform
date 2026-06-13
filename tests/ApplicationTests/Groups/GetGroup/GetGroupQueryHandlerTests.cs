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
        var owner = UserService.ProvisionFromExternalIdentity(
            "auth0|owner-subject",
            "owner@example.com",
            emailVerified: true,
            profilePictureUrl: null,
            DefaultAvatarUrl,
            ServiceRole.User);
        var member = UserService.ProvisionFromExternalIdentity(
            "auth0|member-subject",
            "member@example.com",
            emailVerified: true,
            profilePictureUrl: null,
            DefaultAvatarUrl,
            ServiceRole.User);

        var createResult = GroupService.Create(
            "Public Org",
            "Description",
            GroupJoinPolicy.Open,
            owner.Id,
            DefaultGroupImageUrl);
        var group = createResult.Value.Group;
        var ownerMembership = createResult.Value.OwnerMembership;
        var memberMembership = GroupMembership.Create(group.Id, member.Id, GroupMemberRole.Member);

        await using (var seedContext = CreateContext(databaseName))
        {
            seedContext.Users.AddRange(owner, member);
            seedContext.Groups.Add(group);
            seedContext.GroupMemberships.AddRange(ownerMembership, memberMembership);
            await seedContext.SaveChangesAsync();
        }

        await using var context = CreateContext(databaseName);
        var handler = new GetGroupQueryHandler(context);

        // Act
        var result = await handler.Handle(new GetGroupQuery(group.Id), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Public Org", result.Value.Name);
        Assert.Equal(2, result.Value.MemberCount);
    }

    [Fact]
    public async Task GetGroupQueryHandler_Should_ReturnNotFound_When_GroupIsDeleted()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = UserService.ProvisionFromExternalIdentity(
            "auth0|deleted-owner",
            "deleted@example.com",
            emailVerified: true,
            profilePictureUrl: null,
            DefaultAvatarUrl,
            ServiceRole.User);

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
        var handler = new GetGroupQueryHandler(context);

        // Act
        var result = await handler.Handle(new GetGroupQuery(group.Id), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Groups.Deleted", result.Error.Code);
    }

    private static ApplicationDbContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new ApplicationDbContext(options);
    }
}
