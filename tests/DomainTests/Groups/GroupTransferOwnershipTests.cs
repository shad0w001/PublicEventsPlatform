using Domain.Groups;
using Domain.Groups.Events;

namespace DomainTests.Groups;

public class GroupTransferOwnershipTests
{
    private const string DefaultImageUrl = "/images/default-group.png";
    private static readonly Guid OwnerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid MemberId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OutsiderId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public void Group_Should_TransferOwnership_When_TargetIsMember()
    {
        // Arrange
        var group = Group.Create("Test Org", "", GroupJoinPolicy.Open, OwnerId, DefaultImageUrl).Value.Group;
        group.GroupMemberships.Add(GroupMembership.Create(group.Id, MemberId, GroupMemberRole.Member));

        // Act
        var result = group.TransferOwnership(OwnerId, MemberId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(GroupMemberRole.Administrator, group.GroupMemberships.Single(m => m.UserId == OwnerId).Role);
        Assert.Equal(GroupMemberRole.Owner, group.GroupMemberships.Single(m => m.UserId == MemberId).Role);
        Assert.Equal(2, group.DomainEvents.Count(e => e is GroupMemberRoleChanged));
    }

    [Fact]
    public void Group_Should_ReturnCannotTransferToSelf_When_TargetIsCurrentOwner()
    {
        // Arrange
        var group = Group.Create("Test Org", "", GroupJoinPolicy.Open, OwnerId, DefaultImageUrl).Value.Group;

        // Act
        var result = group.TransferOwnership(OwnerId, OwnerId);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Groups.CannotTransferOwnershipToSelf", result.Error.Code);
    }

    [Fact]
    public void Group_Should_ReturnCannotTransferToNonMember_When_TargetNotMember()
    {
        // Arrange
        var group = Group.Create("Test Org", "", GroupJoinPolicy.Open, OwnerId, DefaultImageUrl).Value.Group;

        // Act
        var result = group.TransferOwnership(OwnerId, OutsiderId);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Groups.CannotTransferOwnershipToNonMember", result.Error.Code);
    }
}
