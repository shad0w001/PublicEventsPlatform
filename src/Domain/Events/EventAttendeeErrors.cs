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
}
