using Application.Abstractions.Data;
using Application.Abstractions.Notifications;
using Application.Notifications.Services;
using Domain.Groups.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Notifications.Handlers;

internal sealed class GroupJoinApplicationSubmittedEmailHandler(
    IApplicationDbContext context,
    NotificationRecipientService recipientService,
    NotificationLinkBuilder linkBuilder,
    IEmailSender emailSender,
    ILogger<GroupJoinApplicationSubmittedEmailHandler> logger) : IGroupJoinApplicationSubmittedEmailHandler
{
    public async Task HandleAsync(
        GroupJoinApplicationSubmitted domainEvent,
        CancellationToken cancellationToken = default)
    {
        var recipients = await recipientService.GetApplicationReviewerEmailsAsync(
            domainEvent.GroupId,
            cancellationToken);

        if (recipients.Count == 0)
        {
            logger.LogWarning(
                "Skipping join application submitted email for group {GroupId}: no reviewer recipients",
                domainEvent.GroupId);
            return;
        }

        var groupName = await context.Groups
            .AsNoTracking()
            .Where(g => g.Id == domainEvent.GroupId)
            .Select(g => g.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "Organization";

        var applicant = await context.Users
            .AsNoTracking()
            .Where(u => u.Id == domainEvent.UserId)
            .Select(u => new { u.Username, u.Email })
            .FirstOrDefaultAsync(cancellationToken);

        var applicantDisplay = applicant is null
            ? "A user"
            : NotificationEmailComposer.FormatApplicantDisplayName(applicant.Username, applicant.Email);

        var groupLink = linkBuilder.Group(domainEvent.GroupId);

        var body = $"""
            A new join application was submitted for "{groupName}".

            Applicant: {applicantDisplay}
            Review: {groupLink}
            """;

        await emailSender.SendAsync(
            new EmailMessage(
                recipients,
                $"New join application — {groupName}",
                body),
            cancellationToken);
    }
}
