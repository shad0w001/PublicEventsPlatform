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
}
