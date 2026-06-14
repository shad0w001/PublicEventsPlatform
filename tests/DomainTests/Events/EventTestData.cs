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

    internal static (Event Event, EventOrganizer Organizer) CreateDraft(EventTier tier = EventTier.Small) =>
        EventService.Create(tier, HostParticipantId).Value;

    internal static void MakePublishReady(Event @event)
    {
        @event.Title = "Test Event";
        @event.Description = "A test event description";
        @event.CategoryId = CategoryId;
        @event.StartTime = new DateTime(2026, 7, 1, 18, 0, 0, DateTimeKind.Utc);
        @event.EndTime = new DateTime(2026, 7, 1, 22, 0, 0, DateTimeKind.Utc);
        @event.TimeZoneId = ValidTimeZoneId;
        @event.AdmissionType = AdmissionType.Free;
        @event.Locations.Add(new EventLocation
        {
            Name = "Main Hall",
            Date = @event.StartTime,
            Kind = EventLocationKind.Physical,
            Address = "123 Main St",
            City = "Sofia"
        });
    }

    internal static EventLocation PhysicalLocation(string name = "Venue") =>
        new()
        {
            Name = name,
            Date = new DateTime(2026, 7, 1, 18, 0, 0, DateTimeKind.Utc),
            Kind = EventLocationKind.Physical,
            Address = "123 Main St",
            City = "Sofia"
        };

    internal static EventLocation VirtualLocation(string name = "Stream") =>
        new()
        {
            Name = name,
            Date = new DateTime(2026, 7, 1, 18, 0, 0, DateTimeKind.Utc),
            Kind = EventLocationKind.Virtual,
            Url = "https://stream.example.com/live"
        };
}
