using Application.Notifications.Handlers;
using Application.Notifications.Services;
using Domain.Groups;
using Domain.Groups.Events;
using Domain.Groups.Services;
using Domain.Users;
using Domain.Users.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace ApplicationTests.Notifications.Handlers;

public class GroupJoinApplicationEmailHandlerTests
{
    [Fact]
    public async Task HandleAsync_Should_NotifyReviewers_When_ApplicationSubmitted()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|join-owner", "join-owner@example.com");
        var moderator = CreateUser("auth0|join-mod", "join-mod@example.com");
        var applicant = CreateUser("auth0|join-applicant", "applicant@example.com", username: "applicant_user");
        var group = SeedGroup(databaseName, owner, [(moderator, GroupMemberRole.Moderator)], applicant);
        var fakeEmailSender = new FakeEmailSender();
        var handler = CreateSubmittedHandler(databaseName, fakeEmailSender);

        // Act
        await handler.HandleAsync(
            new GroupJoinApplicationSubmitted(group.Id, Guid.NewGuid(), applicant.Id),
            CancellationToken.None);

        // Assert
        Assert.NotNull(fakeEmailSender.LastMessage);
        Assert.Equal(2, fakeEmailSender.LastMessage!.To.Count);
        Assert.Contains("join-owner@example.com", fakeEmailSender.LastMessage.To);
        Assert.Contains("join-mod@example.com", fakeEmailSender.LastMessage.To);
        Assert.Contains("applicant_user", fakeEmailSender.LastMessage.Body);
        Assert.Contains($"http://localhost:5173/groups/{group.Id}", fakeEmailSender.LastMessage.Body);
    }

    [Fact]
    public async Task HandleAsync_Should_NotifyApplicant_When_ApplicationApproved()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|approved-owner", "approved-owner@example.com");
        var applicant = CreateUser("auth0|approved-applicant", "approved-applicant@example.com");
        var group = SeedGroup(databaseName, owner, [], applicant);
        var fakeEmailSender = new FakeEmailSender();
        var handler = CreateApprovedHandler(databaseName, fakeEmailSender);

        // Act
        await handler.HandleAsync(
            new GroupJoinApplicationApproved(group.Id, Guid.NewGuid(), applicant.Id),
            CancellationToken.None);

        // Assert
        Assert.NotNull(fakeEmailSender.LastMessage);
        Assert.Equal(["approved-applicant@example.com"], fakeEmailSender.LastMessage!.To);
        Assert.Contains("Application approved", fakeEmailSender.LastMessage.Subject);
        Assert.Contains("Ticket Org", fakeEmailSender.LastMessage.Body);
    }

    [Fact]
    public async Task HandleAsync_Should_NotifyApplicant_When_ApplicationRejected()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|rejected-owner", "rejected-owner@example.com");
        var applicant = CreateUser("auth0|rejected-applicant", "rejected-applicant@example.com");
        var group = SeedGroup(databaseName, owner, [], applicant);
        var fakeEmailSender = new FakeEmailSender();
        var handler = CreateRejectedHandler(databaseName, fakeEmailSender);

        // Act
        await handler.HandleAsync(
            new GroupJoinApplicationRejected(group.Id, Guid.NewGuid(), applicant.Id),
            CancellationToken.None);

        // Assert
        Assert.NotNull(fakeEmailSender.LastMessage);
        Assert.Equal(["rejected-applicant@example.com"], fakeEmailSender.LastMessage!.To);
        Assert.Contains("not approved", fakeEmailSender.LastMessage.Subject);
    }

    private static GroupJoinApplicationSubmittedEmailHandler CreateSubmittedHandler(
        string databaseName,
        FakeEmailSender fakeEmailSender)
    {
        var context = NotificationHandlerTestSupport.CreateContext(databaseName);
        return new GroupJoinApplicationSubmittedEmailHandler(
            context,
            new NotificationRecipientService(context),
            NotificationHandlerTestSupport.CreateLinkBuilder(),
            fakeEmailSender,
            NullLogger<GroupJoinApplicationSubmittedEmailHandler>.Instance);
    }

    private static GroupJoinApplicationApprovedEmailHandler CreateApprovedHandler(
        string databaseName,
        FakeEmailSender fakeEmailSender)
    {
        var context = NotificationHandlerTestSupport.CreateContext(databaseName);
        return new GroupJoinApplicationApprovedEmailHandler(
            context,
            new NotificationRecipientService(context),
            NotificationHandlerTestSupport.CreateLinkBuilder(),
            fakeEmailSender,
            NullLogger<GroupJoinApplicationApprovedEmailHandler>.Instance);
    }

    private static GroupJoinApplicationRejectedEmailHandler CreateRejectedHandler(
        string databaseName,
        FakeEmailSender fakeEmailSender)
    {
        var context = NotificationHandlerTestSupport.CreateContext(databaseName);
        return new GroupJoinApplicationRejectedEmailHandler(
            context,
            new NotificationRecipientService(context),
            NotificationHandlerTestSupport.CreateLinkBuilder(),
            fakeEmailSender,
            NullLogger<GroupJoinApplicationRejectedEmailHandler>.Instance);
    }

    private static Group SeedGroup(
        string databaseName,
        User owner,
        IReadOnlyList<(User User, GroupMemberRole Role)> additionalMembers,
        User? extraUser = null)
    {
        var createResult = GroupService.Create(
            "Ticket Org",
            "",
            GroupJoinPolicy.Open,
            owner.Id,
            "/images/default-group.png");
        var group = createResult.Value.Group;

        using var context = NotificationHandlerTestSupport.CreateContext(databaseName);
        context.Users.Add(owner);
        if (extraUser is not null && extraUser.Id != owner.Id)
        {
            context.Users.Add(extraUser);
        }

        context.Groups.Add(group);
        context.GroupMemberships.Add(createResult.Value.OwnerMembership);

        foreach (var (user, role) in additionalMembers)
        {
            if (user.Id == owner.Id)
            {
                continue;
            }

            context.Users.Add(user);
            context.GroupMemberships.Add(GroupMembership.Create(group.Id, user.Id, role));
        }

        context.SaveChanges();
        return group;
    }

    private static User CreateUser(string subject, string email, string? username = null)
    {
        var user = UserService.ProvisionFromExternalIdentity(
            subject,
            email,
            emailVerified: true,
            profilePictureUrl: null,
            NotificationHandlerTestSupport.DefaultAvatarUrl,
            ServiceRole.User);

        if (username is not null)
        {
            user.Username = username;
        }

        return user;
    }
}
