using Application.Notifications.Handlers;
using Application.Notifications.Services;
using Domain.Groups.Events;
using Microsoft.Extensions.Logging.Abstractions;

namespace ApplicationTests.Notifications.Handlers;

public class GroupVerificationApplicationEmailHandlerTests
{
    [Fact]
    public async Task HandleAsync_Should_NotifyOrganizerPlus_When_ApplicationApproved()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var (group, _) = await Groups.GroupVerificationTestData.SeedEligibleGroupAsync(databaseName);
        var fakeEmailSender = new FakeEmailSender();
        var handler = CreateApprovedHandler(databaseName, fakeEmailSender);

        // Act
        await handler.HandleAsync(
            new GroupVerificationApplicationApproved(group.Id, Guid.NewGuid()),
            CancellationToken.None);

        // Assert
        Assert.NotNull(fakeEmailSender.LastMessage);
        Assert.Contains("verify-owner@example.com", fakeEmailSender.LastMessage!.To);
        Assert.Contains("verified", fakeEmailSender.LastMessage.Subject, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"http://localhost:5173/groups/{group.Id}", fakeEmailSender.LastMessage.Body);
    }

    [Fact]
    public async Task HandleAsync_Should_NotifyOrganizerPlus_When_ApplicationRejected()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var (group, _) = await Groups.GroupVerificationTestData.SeedEligibleGroupAsync(databaseName);
        var fakeEmailSender = new FakeEmailSender();
        var handler = CreateRejectedHandler(databaseName, fakeEmailSender);

        // Act
        await handler.HandleAsync(
            new GroupVerificationApplicationRejected(group.Id, Guid.NewGuid()),
            CancellationToken.None);

        // Assert
        Assert.NotNull(fakeEmailSender.LastMessage);
        Assert.Contains("verify-owner@example.com", fakeEmailSender.LastMessage!.To);
        Assert.Contains("declined", fakeEmailSender.LastMessage.Subject, StringComparison.OrdinalIgnoreCase);
    }

    private static GroupVerificationApplicationApprovedEmailHandler CreateApprovedHandler(
        string databaseName,
        FakeEmailSender emailSender)
    {
        var context = NotificationHandlerTestSupport.CreateContext(databaseName);
        return new GroupVerificationApplicationApprovedEmailHandler(
            context,
            new NotificationRecipientService(context),
            NotificationHandlerTestSupport.CreateLinkBuilder(),
            emailSender,
            NullLogger<GroupVerificationApplicationApprovedEmailHandler>.Instance);
    }

    private static GroupVerificationApplicationRejectedEmailHandler CreateRejectedHandler(
        string databaseName,
        FakeEmailSender emailSender)
    {
        var context = NotificationHandlerTestSupport.CreateContext(databaseName);
        return new GroupVerificationApplicationRejectedEmailHandler(
            context,
            new NotificationRecipientService(context),
            NotificationHandlerTestSupport.CreateLinkBuilder(),
            emailSender,
            NullLogger<GroupVerificationApplicationRejectedEmailHandler>.Instance);
    }
}
