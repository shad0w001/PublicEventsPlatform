using Domain.Groups;
using Domain.Groups.Events;
using Domain.Groups.Services;

namespace DomainTests.Groups;

public class GroupJoinApplicationTests
{
    private const string DefaultImageUrl = "/images/default-group.png";
    private static readonly Guid CreatorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ApplicantId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ModeratorId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTime UtcNow = new(2026, 6, 13, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void GroupService_Should_CreatePendingApplication_When_SubmitJoinApplication()
    {
        // Arrange
        var group = CreateApplicationRequiredGroup();

        // Act
        var result = GroupService.SubmitJoinApplication(group, ApplicantId, UtcNow);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(GroupJoinApplicationStatus.Pending, result.Value.Status);
        Assert.Contains(group.DomainEvents, e => e is GroupJoinApplicationSubmitted);
    }

    [Fact]
    public void GroupService_Should_ReturnPendingApplicationExists_When_DuplicatePendingSubmission()
    {
        // Arrange
        var group = CreateApplicationRequiredGroup();
        GroupService.SubmitJoinApplication(group, ApplicantId, UtcNow);

        // Act
        var result = GroupService.SubmitJoinApplication(group, ApplicantId, UtcNow);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Groups.PendingApplicationExists", result.Error.Code);
    }

    [Fact]
    public void GroupService_Should_ReturnReapplyCooldownActive_When_RejectedWithinCooldown()
    {
        // Arrange
        var group = CreateApplicationRequiredGroup();
        var application = GroupService.SubmitJoinApplication(group, ApplicantId, UtcNow).Value;
        GroupService.RejectApplication(group, application.Id, ModeratorId, UtcNow);

        // Act
        var result = GroupService.SubmitJoinApplication(
            group,
            ApplicantId,
            UtcNow.AddMinutes(30));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Groups.ReapplyCooldownActive", result.Error.Code);
    }

    [Fact]
    public void GroupService_Should_AllowReapply_When_CooldownHasElapsed()
    {
        // Arrange
        var group = CreateApplicationRequiredGroup();
        var application = GroupService.SubmitJoinApplication(group, ApplicantId, UtcNow).Value;
        GroupService.RejectApplication(group, application.Id, ModeratorId, UtcNow);

        // Act
        var result = GroupService.SubmitJoinApplication(
            group,
            ApplicantId,
            UtcNow.AddHours(1));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, group.JoinApplications.Count);
    }

    [Fact]
    public void GroupService_Should_CreateMember_When_ApplicationApproved()
    {
        // Arrange
        var group = CreateApplicationRequiredGroup();
        var application = GroupService.SubmitJoinApplication(group, ApplicantId, UtcNow).Value;

        // Act
        var result = GroupService.ApproveApplication(group, application.Id, ModeratorId, UtcNow);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(GroupMemberRole.Member, result.Value.Role);
        Assert.Contains(group.DomainEvents, e => e is GroupJoinApplicationApproved);
        Assert.Contains(group.DomainEvents, e => e is GroupMemberJoined);
    }

    [Fact]
    public void GroupService_Should_RemoveApplication_When_CancelledByApplicant()
    {
        // Arrange
        var group = CreateApplicationRequiredGroup();
        var application = GroupService.SubmitJoinApplication(group, ApplicantId, UtcNow).Value;

        // Act
        var result = GroupService.CancelApplication(group, application.Id, ApplicantId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(group.JoinApplications);
        Assert.Contains(group.DomainEvents, e => e is GroupJoinApplicationCancelled);
    }

    private static Group CreateApplicationRequiredGroup() =>
        GroupService.Create(
            "Test Org",
            "",
            GroupJoinPolicy.ApplicationRequired,
            CreatorId,
            DefaultImageUrl).Value.Group;
}
