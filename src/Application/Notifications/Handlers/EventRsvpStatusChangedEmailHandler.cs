using Application.Abstractions.Data;
using Application.Abstractions.Notifications;
using Application.Notifications.Services;
using Domain.Events.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Notifications.Handlers;

internal sealed class EventRsvpStatusChangedEmailHandler(
    IApplicationDbContext context,
    NotificationRecipientService recipientService,
    NotificationLinkBuilder linkBuilder,
    IEmailSender emailSender,
    ILogger<EventRsvpStatusChangedEmailHandler> logger) : IEventRsvpStatusChangedEmailHandler
{
    public async Task HandleAsync(
        EventRsvpStatusChanged domainEvent,
        CancellationToken cancellationToken = default)
    {
        var recipients = await recipientService.ResolveParticipantEmailsAsync(
            domainEvent.ParticipantId,
            cancellationToken);

        if (recipients.Count == 0)
        {
            logger.LogWarning(
                "Skipping RSVP email for event {EventId}: no recipients for participant {ParticipantId}",
                domainEvent.EventId,
                domainEvent.ParticipantId);
            return;
        }

        var eventTitle = await context.Events
            .AsNoTracking()
            .Where(e => e.Id == domainEvent.EventId)
            .Select(e => e.Title)
            .FirstOrDefaultAsync(cancellationToken) ?? "Event";

        var statusLabel = NotificationEmailComposer.FormatRsvpStatus(domainEvent.Status);
        var eventLink = linkBuilder.Event(domainEvent.EventId);

        var body = $"""
            Your RSVP for "{eventTitle}" has been updated.

            Status: {statusLabel}
            Event: {eventLink}
            """;

        await emailSender.SendAsync(
            new EmailMessage(
                recipients,
                $"RSVP updated — {eventTitle}",
                body),
            cancellationToken);
    }
}
