using Domain.Groups;
using Domain.Groups.Events;
using Domain.Groups.Services;

namespace DomainTests.Groups;

public class GroupChangeMemberRoleTests
{
    private const string DefaultImageUrl = "/images/default-group.png";
    private static readonly Guid OwnerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid MemberId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void GroupService_Should_ChangeMemberRole_When_TargetIsNotOwner()
    {
        // Arrange
        var group = GroupService.Create("Test Org", "", GroupJoinPolicy.Open, OwnerId, DefaultImageUrl).Value.Group;
        group.GroupMemberships.Add(GroupMembership.Create(group.Id, MemberId, GroupMemberRole.Member));

        // Act
        var result = GroupService.ChangeMemberRole(group, MemberId, GroupMemberRole.Organizer);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(GroupMemberRole.Organizer, group.GroupMemberships.Single(m => m.UserId == MemberId).Role);
        Assert.Contains(group.DomainEvents, e =>
            e is GroupMemberRoleChanged changed &&
            changed.UserId == MemberId &&
            changed.PreviousRole == GroupMemberRole.Member &&
            changed.NewRole == GroupMemberRole.Organizer);
    }

    [Fact]
    public void GroupService_Should_ReturnCannotChangeOwnerRole_When_TargetIsOwner()
    {
        // Arrange
        var group = GroupService.Create("Test Org", "", GroupJoinPolicy.Open, OwnerId, DefaultImageUrl).Value.Group;

        // Act
        var result = GroupService.ChangeMemberRole(group, OwnerId, GroupMemberRole.Administrator);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Groups.CannotChangeOwnerRole", result.Error.Code);
    }

    [Fact]
    public void GroupService_Should_ReturnCannotChangeOwnerRole_When_NewRoleIsOwner()
    {
        // Arrange
        var group = GroupService.Create("Test Org", "", GroupJoinPolicy.Open, OwnerId, DefaultImageUrl).Value.Group;
        group.GroupMemberships.Add(GroupMembership.Create(group.Id, MemberId, GroupMemberRole.Member));

        // Act
        var result = GroupService.ChangeMemberRole(group, MemberId, GroupMemberRole.Owner);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Groups.CannotChangeOwnerRole", result.Error.Code);
    }
}
