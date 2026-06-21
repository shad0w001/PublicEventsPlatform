using SharedKernel;

namespace Application.Events;

public static class EventDiscoveryErrors
{
    public static readonly Error InvalidDateRange = Error.Validation(
        "Events.Discovery.InvalidDateRange",
        "startFrom must be before or equal to startTo");
}
