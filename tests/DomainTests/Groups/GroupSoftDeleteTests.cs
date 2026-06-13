using Domain.Groups;
using Domain.Groups.Events;

namespace DomainTests.Groups;

public class GroupSoftDeleteTests
{
    private const string DefaultImageUrl = "/images/default-group.png";
    private static readonly Guid CreatorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ApplicantId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTime UtcNow = new(2026, 6, 13, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Group_Should_SetDeletedAtAndClearApplications_When_SoftDeleted()
    {
        // Arrange
        var group = Group.Create(
            "Test Org",
            "",
            GroupJoinPolicy.ApplicationRequired,
            CreatorId,
            DefaultImageUrl).Value.Group;
        group.SubmitJoinApplication(ApplicantId, UtcNow);

        // Act
        var result = group.SoftDelete();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(group.DeletedAt);
        Assert.True(group.IsDeleted);
        Assert.Empty(group.JoinApplications);
        Assert.Single(group.GroupMemberships);
    }

    [Fact]
    public void Group_Should_BeIdempotent_When_SoftDeletedTwice()
    {
        // Arrange
        var group = Group.Create("Test Org", "", GroupJoinPolicy.Open, CreatorId, DefaultImageUrl).Value.Group;
        group.SoftDelete();
        var firstDeletedAt = group.DeletedAt;
        group.ClearDomainEvents();

        // Act
        var result = group.SoftDelete();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(firstDeletedAt, group.DeletedAt);
        Assert.Empty(group.DomainEvents);
    }

    [Fact]
    public void Group_Should_RaiseSoftDeletedEvent_When_SoftDeleted()
    {
        // Arrange
        var group = Group.Create("Test Org", "", GroupJoinPolicy.Open, CreatorId, DefaultImageUrl).Value.Group;

        // Act
        group.SoftDelete();

        // Assert
        Assert.Contains(group.DomainEvents, e => e is GroupSoftDeleted);
    }
}
