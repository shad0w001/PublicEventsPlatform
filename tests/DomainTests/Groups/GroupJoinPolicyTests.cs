using Domain.Groups;
using Domain.Groups.Events;

namespace DomainTests.Groups;

public class GroupJoinPolicyTests
{
    private const string DefaultImageUrl = "/images/default-group.png";
    private static readonly Guid CreatorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ApplicantId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTime UtcNow = new(2026, 6, 13, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Group_Should_ClearApplicationsAndRaiseEvent_When_JoinPolicyChanges()
    {
        // Arrange
        var createResult = Group.Create(
            "Test Org",
            "",
            GroupJoinPolicy.ApplicationRequired,
            CreatorId,
            DefaultImageUrl);
        var group = createResult.Value.Group;
        group.SubmitJoinApplication(ApplicantId, UtcNow);
        createResult.Value.Group.ClearDomainEvents();

        // Act
        var result = group.ChangeJoinPolicy(GroupJoinPolicy.Open);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(GroupJoinPolicy.Open, group.JoinPolicy);
        Assert.Empty(group.JoinApplications);
        Assert.Contains(group.DomainEvents, e => e is GroupJoinPolicyChanged changed
            && changed.NewPolicy == GroupJoinPolicy.Open);
    }

    [Fact]
    public void Group_Should_SucceedWithoutEvent_When_JoinPolicyIsUnchanged()
    {
        // Arrange
        var group = Group.Create("Test Org", "", GroupJoinPolicy.Open, CreatorId, DefaultImageUrl).Value.Group;
        group.ClearDomainEvents();

        // Act
        var result = group.ChangeJoinPolicy(GroupJoinPolicy.Open);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(group.DomainEvents);
    }
}
