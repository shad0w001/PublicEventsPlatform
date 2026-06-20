using Domain.Events;

namespace Application.Notifications;

internal static class NotificationEmailComposer
{
    public static string FormatRsvpStatus(EventAttendeeStatus status) => status switch
    {
        EventAttendeeStatus.Going => "Going",
        EventAttendeeStatus.Interested => "Interested",
        EventAttendeeStatus.NotGoing => "Not going",
        _ => status.ToString()
    };

    public static string FormatApplicantDisplayName(string? username, string email) =>
        string.IsNullOrWhiteSpace(username) ? email : username;

    public static string FormatEventCancelledFreeBody(string eventTitle, string eventLink) =>
        $"""
            "{eventTitle}" has been cancelled.

            Event: {eventLink}
            """;

    public static string FormatEventCancelledPaidBody(string eventTitle, string orderLink) =>
        $"""
            "{eventTitle}" has been cancelled.

            A refund will be processed for your paid order. You do not need to take any action.

            Order: {orderLink}
            """;
}
