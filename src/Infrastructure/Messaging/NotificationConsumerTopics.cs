namespace Infrastructure.Messaging;

internal static class NotificationConsumerNames
{
    public const string TicketPurchaseCompleted = "email.ticket-purchase-completed";
    public const string EventRsvpStatusChanged = "email.event-rsvp-status-changed";
    public const string EventCancelled = "email.event-cancelled";
    public const string GroupJoinApplicationSubmitted = "email.group-join-application-submitted";
    public const string GroupJoinApplicationApproved = "email.group-join-application-approved";
    public const string GroupJoinApplicationRejected = "email.group-join-application-rejected";
}

internal static class NotificationConsumerTopics
{
    public const string TicketPurchaseCompleted = "domain.ticket-purchase-completed";
    public const string EventRsvpStatusChanged = "domain.event-rsvp-status-changed";
    public const string EventCancelled = "domain.event-cancelled";
    public const string GroupJoinApplicationSubmitted = "domain.group-join-application-submitted";
    public const string GroupJoinApplicationApproved = "domain.group-join-application-approved";
    public const string GroupJoinApplicationRejected = "domain.group-join-application-rejected";

    public static readonly string[] NotificationTopics =
    [
        TicketPurchaseCompleted,
        EventRsvpStatusChanged,
        EventCancelled,
        GroupJoinApplicationSubmitted,
        GroupJoinApplicationApproved,
        GroupJoinApplicationRejected
    ];
}
