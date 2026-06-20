using Application.Abstractions.Data;
using Application.Abstractions.Notifications;
using Application.Notifications.Services;
using Domain.Events;
using Domain.Events.Events;
using Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Notifications.Handlers;

internal sealed class EventCancelledEmailHandler(
    IApplicationDbContext context,
    NotificationRecipientService recipientService,
    NotificationLinkBuilder linkBuilder,
    IEmailSender emailSender,
    ILogger<EventCancelledEmailHandler> logger) : IEventCancelledEmailHandler
{
    public async Task HandleAsync(
        EventCancelled domainEvent,
        CancellationToken cancellationToken = default)
    {
        var eventInfo = await context.Events
            .AsNoTracking()
            .Where(e => e.Id == domainEvent.EventId)
            .Select(e => new { e.Title, e.AdmissionType, e.Status })
            .FirstOrDefaultAsync(cancellationToken);

        if (eventInfo is null)
        {
            logger.LogWarning(
                "Skipping event cancelled email: event {EventId} not found",
                domainEvent.EventId);
            return;
        }

        if (eventInfo.Status != EventStatus.Cancelled)
        {
            logger.LogWarning(
                "Skipping event cancelled email: event {EventId} is not cancelled",
                domainEvent.EventId);
            return;
        }

        var subject = $"Event cancelled — {eventInfo.Title}";

        if (eventInfo.AdmissionType == AdmissionType.Paid)
        {
            var paidOrders = await context.Orders
                .AsNoTracking()
                .Where(o => o.EventId == domainEvent.EventId && o.Status == OrderStatus.Paid)
                .Select(o => new { o.Id, o.ParticipantId })
                .ToListAsync(cancellationToken);

            foreach (var order in paidOrders)
            {
                await SendPaidCancellationEmailAsync(
                    order.Id,
                    order.ParticipantId,
                    eventInfo.Title,
                    subject,
                    cancellationToken);
            }

            return;
        }

        var goingParticipantIds = await context.EventAttendees
            .AsNoTracking()
            .Where(a => a.EventId == domainEvent.EventId && a.Status == EventAttendeeStatus.Going)
            .Select(a => a.ParticipantId)
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var participantId in goingParticipantIds)
        {
            await SendFreeCancellationEmailAsync(
                domainEvent.EventId,
                participantId,
                eventInfo.Title,
                subject,
                cancellationToken);
        }
    }

    private async Task SendPaidCancellationEmailAsync(
        Guid orderId,
        Guid participantId,
        string eventTitle,
        string subject,
        CancellationToken cancellationToken)
    {
        var recipients = await recipientService.ResolveParticipantEmailsAsync(
            participantId,
            cancellationToken);

        if (recipients.Count == 0)
        {
            logger.LogWarning(
                "Skipping event cancelled email for order {OrderId}: no recipients for participant {ParticipantId}",
                orderId,
                participantId);
            return;
        }

        var body = NotificationEmailComposer.FormatEventCancelledPaidBody(
            eventTitle,
            linkBuilder.Order(orderId));

        await emailSender.SendAsync(
            new EmailMessage(recipients, subject, body),
            cancellationToken);
    }

    private async Task SendFreeCancellationEmailAsync(
        Guid eventId,
        Guid participantId,
        string eventTitle,
        string subject,
        CancellationToken cancellationToken)
    {
        var recipients = await recipientService.ResolveParticipantEmailsAsync(
            participantId,
            cancellationToken);

        if (recipients.Count == 0)
        {
            logger.LogWarning(
                "Skipping event cancelled email for event {EventId}: no recipients for participant {ParticipantId}",
                eventId,
                participantId);
            return;
        }

        var body = NotificationEmailComposer.FormatEventCancelledFreeBody(
            eventTitle,
            linkBuilder.Event(eventId));

        await emailSender.SendAsync(
            new EmailMessage(recipients, subject, body),
            cancellationToken);
    }
}
