using Domain.Groups;
using Domain.Groups.Events;
using SharedKernel;

namespace DomainTests.Groups;

public class GroupCreateTests
{
    private const string DefaultImageUrl = "/images/default-group.png";
    private static readonly Guid CreatorId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void Group_Should_CreateWithOwnerMembership_When_InputIsValid()
    {
        // Arrange
        const string name = "  Test Org  ";

        // Act
        var result = Group.Create(
            name,
            description: null,
            GroupJoinPolicy.Open,
            CreatorId,
            DefaultImageUrl);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Test Org", result.Value.Group.Name);
        Assert.Equal(string.Empty, result.Value.Group.Description);
        Assert.Equal(DefaultImageUrl, result.Value.Group.ProfileImageUrl);
        Assert.Equal(GroupJoinPolicy.Open, result.Value.Group.JoinPolicy);
        Assert.Equal(GroupMemberRole.Owner, result.Value.OwnerMembership.Role);
        Assert.Equal(CreatorId, result.Value.OwnerMembership.UserId);
    }

    [Fact]
    public void Group_Should_RaiseGroupCreatedEvent_When_Created()
    {
        // Arrange — valid create inputs
        // Act
        var result = Group.Create("Test Org", "", GroupJoinPolicy.Open, CreatorId, DefaultImageUrl);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Contains(
            result.Value.Group.DomainEvents,
            e => e is GroupCreated created && created.GroupId == result.Value.Group.Id);
    }

    [Fact]
    public void Group_Should_ReturnInvalidName_When_NameIsEmpty()
    {
        // Arrange
        const string name = "   ";

        // Act
        var result = Group.Create(name, "", GroupJoinPolicy.Open, CreatorId, DefaultImageUrl);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Groups.InvalidName", result.Error.Code);
    }

    [Fact]
    public void Group_Should_ReturnNameTooLong_When_NameExceedsMaxLength()
    {
        // Arrange
        var name = new string('a', GroupConstants.NameMaxLength + 1);

        // Act
        var result = Group.Create(name, "", GroupJoinPolicy.Open, CreatorId, DefaultImageUrl);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Groups.NameTooLong", result.Error.Code);
    }
}
