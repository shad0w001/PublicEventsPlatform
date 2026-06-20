using Application.Abstractions.Data;
using Application.Abstractions.Notifications;
using Application.Notifications.Services;
using Domain.Groups.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Notifications.Handlers;

internal sealed class GroupJoinApplicationRejectedEmailHandler(
    IApplicationDbContext context,
    NotificationRecipientService recipientService,
    NotificationLinkBuilder linkBuilder,
    IEmailSender emailSender,
    ILogger<GroupJoinApplicationRejectedEmailHandler> logger) : IGroupJoinApplicationRejectedEmailHandler
{
    public async Task HandleAsync(
        GroupJoinApplicationRejected domainEvent,
        CancellationToken cancellationToken = default)
    {
        var recipient = await recipientService.GetUserEmailAsync(domainEvent.UserId, cancellationToken);
        if (recipient is null)
        {
            logger.LogWarning(
                "Skipping join application rejected email for user {UserId}: no email",
                domainEvent.UserId);
            return;
        }

        var groupName = await context.Groups
            .AsNoTracking()
            .Where(g => g.Id == domainEvent.GroupId)
            .Select(g => g.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "Organization";

        var groupLink = linkBuilder.Group(domainEvent.GroupId);

        var body = $"""
            Your application to join "{groupName}" was not approved.

            Organization: {groupLink}
            """;

        await emailSender.SendAsync(
            new EmailMessage(
                [recipient],
                $"Application not approved — {groupName}",
                body),
            cancellationToken);
    }
}
