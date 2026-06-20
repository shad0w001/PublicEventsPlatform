using SharedKernel;

namespace Domain.Events;

public static class EventAttendeeErrors
{
    public static readonly Error PaidAdmissionNotAllowed = Error.Validation(
        "EventAttendees.PaidAdmissionNotAllowed",
        "RSVP is only available on free-admission events");

    public static readonly Error HostMustRemainGoing = Error.Validation(
        "EventAttendees.HostMustRemainGoing",
        "The event host RSVP status must remain going");

    public static readonly Error FreeAdmissionNotAllowed = Error.Validation(
        "EventAttendees.FreeAdmissionNotAllowed",
        "Paid attendance rows are only allowed on paid-admission events");

    public static readonly Error InvalidTicketCountDelta = Error.Validation(
        "EventAttendees.InvalidTicketCountDelta",
        "Ticket count delta must be greater than zero");
}
