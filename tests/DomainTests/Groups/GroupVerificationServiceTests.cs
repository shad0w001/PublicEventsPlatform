using Domain.Groups;
using Domain.Groups.Events;
using Domain.Groups.Services;

namespace DomainTests.Groups;

public class GroupVerificationServiceTests
{
    private const string DefaultImageUrl = "/images/default-group.png";
    private static readonly Guid OwnerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SubmitterId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid AdminId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTime UtcNow = new(2026, 6, 21, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void GroupVerificationService_Should_CreatePendingApplication_When_EligibilityMet()
    {
        // Arrange
        var group = CreateGroup();

        // Act
        var result = GroupVerificationService.SubmitApplication(
            group,
            SubmitterId,
            GroupVerificationConstants.MinVerifiedMembers,
            GroupVerificationConstants.MinCompletedEvents,
            UtcNow);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(GroupVerificationApplicationStatus.Pending, result.Value.Status);
        Assert.Equal(SubmitterId, result.Value.SubmittedByUserId);
    }

    [Fact]
    public void GroupVerificationService_Should_ReturnInsufficientVerifiedMembers_When_BelowThreshold()
    {
        // Arrange
        var group = CreateGroup();

        // Act
        var result = GroupVerificationService.SubmitApplication(
            group,
            SubmitterId,
            GroupVerificationConstants.MinVerifiedMembers - 1,
            GroupVerificationConstants.MinCompletedEvents,
            UtcNow);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Groups.InsufficientVerifiedMembers", result.Error.Code);
    }

    [Fact]
    public void GroupVerificationService_Should_ReturnInsufficientCompletedEvents_When_BelowThreshold()
    {
        // Arrange
        var group = CreateGroup();

        // Act
        var result = GroupVerificationService.SubmitApplication(
            group,
            SubmitterId,
            GroupVerificationConstants.MinVerifiedMembers,
            GroupVerificationConstants.MinCompletedEvents - 1,
            UtcNow);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Groups.InsufficientCompletedEvents", result.Error.Code);
    }

    [Fact]
    public void GroupVerificationService_Should_ReturnAlreadyVerified_When_GroupIsVerified()
    {
        // Arrange
        var group = CreateGroup();
        group.IsVerified = true;

        // Act
        var result = GroupVerificationService.SubmitApplication(
            group,
            SubmitterId,
            GroupVerificationConstants.MinVerifiedMembers,
            GroupVerificationConstants.MinCompletedEvents,
            UtcNow);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Groups.AlreadyVerified", result.Error.Code);
    }

    [Fact]
    public void GroupVerificationService_Should_ReturnPendingApplicationExists_When_PendingAlreadyExists()
    {
        // Arrange
        var group = CreateGroup();
        GroupVerificationService.SubmitApplication(
            group,
            SubmitterId,
            GroupVerificationConstants.MinVerifiedMembers,
            GroupVerificationConstants.MinCompletedEvents,
            UtcNow);

        // Act
        var result = GroupVerificationService.SubmitApplication(
            group,
            OwnerId,
            GroupVerificationConstants.MinVerifiedMembers,
            GroupVerificationConstants.MinCompletedEvents,
            UtcNow);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Groups.VerificationPendingApplicationExists", result.Error.Code);
    }

    [Fact]
    public void GroupVerificationService_Should_ReturnReapplyCooldownActive_When_RejectedWithinCooldown()
    {
        // Arrange
        var group = CreateGroup();
        var application = GroupVerificationService.SubmitApplication(
            group,
            SubmitterId,
            GroupVerificationConstants.MinVerifiedMembers,
            GroupVerificationConstants.MinCompletedEvents,
            UtcNow).Value;
        GroupVerificationService.RejectApplication(group, application.Id, AdminId, UtcNow);

        // Act
        var result = GroupVerificationService.SubmitApplication(
            group,
            SubmitterId,
            GroupVerificationConstants.MinVerifiedMembers,
            GroupVerificationConstants.MinCompletedEvents,
            UtcNow.AddMinutes(30));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Groups.VerificationReapplyCooldownActive", result.Error.Code);
    }

    [Fact]
    public void GroupVerificationService_Should_SetVerifiedFlags_When_ApplicationApproved()
    {
        // Arrange
        var group = CreateGroup();
        var application = GroupVerificationService.SubmitApplication(
            group,
            SubmitterId,
            GroupVerificationConstants.MinVerifiedMembers,
            GroupVerificationConstants.MinCompletedEvents,
            UtcNow).Value;

        // Act
        var result = GroupVerificationService.ApproveApplication(group, application.Id, AdminId, UtcNow);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(group.IsVerified);
        Assert.Equal(UtcNow, group.VerifiedAt);
        Assert.Contains(group.DomainEvents, e => e is GroupVerificationApplicationApproved);
    }

    [Fact]
    public void GroupVerificationService_Should_RaiseRejectedEvent_When_ApplicationRejected()
    {
        // Arrange
        var group = CreateGroup();
        var application = GroupVerificationService.SubmitApplication(
            group,
            SubmitterId,
            GroupVerificationConstants.MinVerifiedMembers,
            GroupVerificationConstants.MinCompletedEvents,
            UtcNow).Value;

        // Act
        var result = GroupVerificationService.RejectApplication(group, application.Id, AdminId, UtcNow);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(group.IsVerified);
        Assert.Contains(group.DomainEvents, e => e is GroupVerificationApplicationRejected);
    }

    private static Group CreateGroup() =>
        GroupService.Create(
            "Verified Org Candidate",
            "",
            GroupJoinPolicy.Open,
            OwnerId,
            DefaultImageUrl).Value.Group;
}
