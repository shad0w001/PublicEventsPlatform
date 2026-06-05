using Domain.Events;
using Domain.Users;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureTests;

public class ApplicationDbContextTests
{
    [Fact]
    public void EventAttendee_should_not_have_shadow_EventId1_property()
    {
        using var context = CreateContext();

        var entityType = context.Model.FindEntityType(typeof(EventAttendee));

        Assert.NotNull(entityType);
        Assert.Null(entityType.FindProperty("EventId1"));
    }

    [Fact]
    public async Task Can_persist_user_event_organizer_and_attendee_round_trip()
    {
        var databaseName = Guid.NewGuid().ToString();
        Guid userId;
        Guid eventId;

        await using (var context = CreateContext(databaseName))
        {
            var user = new User
            {
                Username = "testuser",
                Email = "test@example.com",
                PasswordHash = "hash",
                ProfilePictureUrl = "https://example.com/pic.jpg",
                LastActive = DateTime.UtcNow
            };

            var evt = new Event
            {
                Title = "Test Event",
                Description = "Test description",
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow.AddHours(2),
                Status = EventStatus.Draft,
                LocationType = EventLocationType.Physical,
                Locations = [],
                Organizers = [],
                Attendees = []
            };

            context.Users.Add(user);
            context.Events.Add(evt);
            await context.SaveChangesAsync();

            userId = user.Id;
            eventId = evt.Id;

            context.EventOrganizers.Add(new EventOrganizer
            {
                EventId = evt.Id,
                ParticipantId = user.Id,
                Event = evt,
                Participant = user
            });

            context.EventAttendees.Add(new EventAttendee
            {
                EventId = evt.Id,
                ParticipantId = user.Id,
                Status = EventAttendeeStatus.Invited,
                RegisteredAt = null,
                Event = evt,
                Participant = user
            });

            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext(databaseName))
        {
            var loadedEvent = await context.Events
                .Include(e => e.Organizers)
                .Include(e => e.Attendees)
                .SingleAsync(e => e.Id == eventId);

            Assert.Single(loadedEvent.Organizers);
            Assert.Equal(userId, loadedEvent.Organizers[0].ParticipantId);

            Assert.Single(loadedEvent.Attendees);
            Assert.Equal(userId, loadedEvent.Attendees[0].ParticipantId);
            Assert.Null(loadedEvent.Attendees[0].RegisteredAt);
            Assert.Equal(EventAttendeeStatus.Invited, loadedEvent.Attendees[0].Status);
        }
    }

    private static ApplicationDbContext CreateContext(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }
}
