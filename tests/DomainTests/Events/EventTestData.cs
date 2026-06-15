using Domain.Events;
using Domain.Events.EventLocations;
using Domain.Events.Services;

namespace DomainTests.Events;

internal static class EventTestData
{
    internal static readonly Guid HostParticipantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    internal static readonly Guid CategoryId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    internal static readonly Guid ActingUserId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    internal const string ValidTimeZoneId = "Europe/Sofia";

    internal static readonly DateTime DefaultEventStart = new(2026, 7, 1, 18, 0, 0, DateTimeKind.Utc);
    internal static readonly DateTime DefaultEventEnd = new(2026, 7, 1, 22, 0, 0, DateTimeKind.Utc);

    internal const string DefaultTitle = "Test Event";

    internal static (Event Event, EventOrganizer Organizer) CreateDraft(EventTier tier = EventTier.Small) =>
        EventService.Create(tier, DefaultTitle, HostParticipantId).Value;

    internal static void MakePublishReady(Event @event)
    {
        @event.Title = "Test Event";
        @event.Description = "A test event description";
        @event.CategoryId = CategoryId;
        @event.StartTime = DefaultEventStart;
        @event.EndTime = DefaultEventEnd;
        @event.TimeZoneId = ValidTimeZoneId;
        @event.AdmissionType = AdmissionType.Free;
        @event.Locations.Add(new EventLocation
        {
            Name = "Main Hall",
            Kind = EventLocationKind.Physical,
            Address = "123 Main St",
            City = "Sofia"
        });
    }

    internal static EventLocation PhysicalLocation(
        string name = "Venue",
        DateTime? startsAt = null,
        DateTime? endsAt = null) =>
        new()
        {
            Name = name,
            StartsAt = startsAt,
            EndsAt = endsAt,
            Kind = EventLocationKind.Physical,
            Address = "123 Main St",
            City = "Sofia"
        };

    internal static EventLocation PhysicalLocationWithCoordinates(string name = "Map Pin") =>
        new()
        {
            Name = name,
            Kind = EventLocationKind.Physical,
            Latitude = 42.6977,
            Longitude = 23.3219
        };

    internal static EventLocation VirtualLocation(string name = "Stream") =>
        new()
        {
            Name = name,
            Kind = EventLocationKind.Virtual,
            Url = "https://stream.example.com/live"
        };
}
