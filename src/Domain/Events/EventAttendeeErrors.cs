using SharedKernel;

namespace Domain.Events;

public static class EventAttendeeErrors
{
    public static readonly Error PaidAdmissionNotAllowed = Error.Validation(
        "EventAttendees.PaidAdmissionNotAllowed",
        "RSVP is only available on free-admission events");

    public static readonly Error HostCannotSetNotGoing = Error.Validation(
        "EventAttendees.HostCannotSetNotGoing",
        "The event host cannot set their RSVP status to not going");
}
