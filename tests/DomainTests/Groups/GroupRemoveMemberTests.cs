using Domain.Groups;
using Domain.Groups.Events;
using Domain.Groups.Services;

namespace DomainTests.Groups;

public class GroupRemoveMemberTests
{
    private const string DefaultImageUrl = "/images/default-group.png";
    private static readonly Guid OwnerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AdminId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ModeratorId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid MemberId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public void GroupService_Should_RemoveMember_When_UserLeavesGroup()
    {
        // Arrange
        var group = GroupService.Create("Test Org", "", GroupJoinPolicy.Open, OwnerId, DefaultImageUrl).Value.Group;
        group.GroupMemberships.Add(GroupMembership.Create(group.Id, MemberId, GroupMemberRole.Member));

        // Act
        var result = GroupService.LeaveOrRemoveMember(group, MemberId, MemberId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(group.GroupMemberships, m => m.UserId == MemberId);
        Assert.Contains(group.DomainEvents, e => e is GroupMemberLeft left && left.UserId == MemberId);
    }

    [Fact]
    public void GroupService_Should_ReturnOwnerCannotLeave_When_OwnerTriesToLeave()
    {
        // Arrange
        var group = GroupService.Create("Test Org", "", GroupJoinPolicy.Open, OwnerId, DefaultImageUrl).Value.Group;

        // Act
        var result = GroupService.LeaveOrRemoveMember(group, OwnerId, OwnerId);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Groups.OwnerCannotLeave", result.Error.Code);
    }

    [Fact]
    public void GroupService_Should_RemoveMember_When_OwnerKicksMember()
    {
        // Arrange
        var group = GroupService.Create("Test Org", "", GroupJoinPolicy.Open, OwnerId, DefaultImageUrl).Value.Group;
        group.GroupMemberships.Add(GroupMembership.Create(group.Id, MemberId, GroupMemberRole.Member));

        // Act
        var result = GroupService.LeaveOrRemoveMember(group, OwnerId, MemberId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(group.GroupMemberships, m => m.UserId == MemberId);
    }

    [Fact]
    public void GroupService_Should_ReturnCannotRemoveMember_When_ModeratorKicksAdministrator()
    {
        // Arrange
        var group = GroupService.Create("Test Org", "", GroupJoinPolicy.Open, OwnerId, DefaultImageUrl).Value.Group;
        group.GroupMemberships.Add(GroupMembership.Create(group.Id, AdminId, GroupMemberRole.Administrator));
        group.GroupMemberships.Add(GroupMembership.Create(group.Id, ModeratorId, GroupMemberRole.Moderator));

        // Act
        var result = GroupService.LeaveOrRemoveMember(group, ModeratorId, AdminId);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Groups.CannotRemoveMember", result.Error.Code);
    }
}
