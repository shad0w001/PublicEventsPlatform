using Application.Abstractions.Data;
using Application.Abstractions.Notifications;
using Application.Notifications.Services;
using Domain.Tickets.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Notifications.Handlers;

internal sealed class TicketPurchaseCompletedEmailHandler(
    IApplicationDbContext context,
    NotificationRecipientService recipientService,
    NotificationLinkBuilder linkBuilder,
    IEmailSender emailSender,
    ILogger<TicketPurchaseCompletedEmailHandler> logger) : ITicketPurchaseCompletedEmailHandler
{
    public async Task HandleAsync(
        TicketPurchaseCompleted domainEvent,
        CancellationToken cancellationToken = default)
    {
        var recipients = await recipientService.ResolveParticipantEmailsAsync(
            domainEvent.ParticipantId,
            cancellationToken);

        if (recipients.Count == 0)
        {
            logger.LogWarning(
                "Skipping ticket purchase email for order {OrderId}: no recipients for participant {ParticipantId}",
                domainEvent.OrderId,
                domainEvent.ParticipantId);
            return;
        }

        var eventTitle = await context.Events
            .AsNoTracking()
            .Where(e => e.Id == domainEvent.EventId)
            .Select(e => e.Title)
            .FirstOrDefaultAsync(cancellationToken) ?? "Event";

        var ticketIds = await context.Tickets
            .AsNoTracking()
            .Where(t => t.OrderId == domainEvent.OrderId)
            .OrderBy(t => t.Id)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        var ticketLines = ticketIds
            .Select(id => $"- {linkBuilder.Ticket(id)}")
            .ToList();

        var body = $"""
            Your ticket purchase for "{eventTitle}" is confirmed.

            Order: {linkBuilder.Order(domainEvent.OrderId)}
            Tickets ({domainEvent.TicketCount}):
            {string.Join(Environment.NewLine, ticketLines)}
            """;

        await emailSender.SendAsync(
            new EmailMessage(
                recipients,
                $"Ticket purchase confirmed — {eventTitle}",
                body),
            cancellationToken);
    }
}
