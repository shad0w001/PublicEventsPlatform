using Application.Abstractions.Data;
using Application.Abstractions.Notifications;
using Application.Notifications.Services;
using Domain.Groups.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Notifications.Handlers;

internal sealed class GroupVerificationApplicationApprovedEmailHandler(
    IApplicationDbContext context,
    NotificationRecipientService recipientService,
    NotificationLinkBuilder linkBuilder,
    IEmailSender emailSender,
    ILogger<GroupVerificationApplicationApprovedEmailHandler> logger)
    : IGroupVerificationApplicationApprovedEmailHandler
{
    public async Task HandleAsync(
        GroupVerificationApplicationApproved domainEvent,
        CancellationToken cancellationToken = default)
    {
        var recipients = await recipientService.GetOrganizerPlusEmailsAsync(
            domainEvent.GroupId,
            cancellationToken);

        if (recipients.Count == 0)
        {
            logger.LogWarning(
                "Skipping verification approved email for group {GroupId}: no Organizer+ recipients",
                domainEvent.GroupId);
            return;
        }

        var groupName = await context.Groups
            .AsNoTracking()
            .Where(g => g.Id == domainEvent.GroupId)
            .Select(g => g.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "Organization";

        var groupLink = linkBuilder.Group(domainEvent.GroupId);

        var body = $"""
            Your organization "{groupName}" has been verified.

            Organization: {groupLink}
            """;

        await emailSender.SendAsync(
            new EmailMessage(
                recipients,
                $"Organization verified — {groupName}",
                body),
            cancellationToken);
    }
}
