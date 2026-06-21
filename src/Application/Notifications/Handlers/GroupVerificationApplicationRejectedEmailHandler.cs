using Application.Abstractions.Data;
using Application.Abstractions.Notifications;
using Application.Notifications.Services;
using Domain.Groups.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Notifications.Handlers;

internal sealed class GroupVerificationApplicationRejectedEmailHandler(
    IApplicationDbContext context,
    NotificationRecipientService recipientService,
    NotificationLinkBuilder linkBuilder,
    IEmailSender emailSender,
    ILogger<GroupVerificationApplicationRejectedEmailHandler> logger)
    : IGroupVerificationApplicationRejectedEmailHandler
{
    public async Task HandleAsync(
        GroupVerificationApplicationRejected domainEvent,
        CancellationToken cancellationToken = default)
    {
        var recipients = await recipientService.GetOrganizerPlusEmailsAsync(
            domainEvent.GroupId,
            cancellationToken);

        if (recipients.Count == 0)
        {
            logger.LogWarning(
                "Skipping verification rejected email for group {GroupId}: no Organizer+ recipients",
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
            Your verification application for "{groupName}" was not approved.

            You may re-apply after a one-hour cooldown if eligibility requirements are met.

            Organization: {groupLink}
            """;

        await emailSender.SendAsync(
            new EmailMessage(
                recipients,
                $"Verification application declined — {groupName}",
                body),
            cancellationToken);
    }
}
