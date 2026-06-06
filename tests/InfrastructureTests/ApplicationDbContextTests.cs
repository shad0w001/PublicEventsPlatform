using Domain.Events;
using Domain.Users;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureTests;

public class ApplicationDbContextTests
{
    [Fact]
    public void EventAttendeeModel_Should_NotContainShadowEventId1_When_ApplicationDbContextModelIsBuilt()
    {
        // Arrange
        using var context = CreateContext();

        // Act
        var entityType = context.Model.FindEntityType(typeof(EventAttendee));
        var shadowProperty = entityType?.FindProperty("EventId1");

        // Assert
        Assert.NotNull(entityType);
        Assert.Null(shadowProperty);
    }

    [Fact]
    public async Task Event_Should_ReturnPersistedOrganizersAndAttendees_When_UserEventRelationsAreSaved()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();

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

        // Act
        await using (var context = CreateContext(databaseName))
        {
            context.Users.Add(user);
            context.Events.Add(evt);
            await context.SaveChangesAsync();

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

        Event loadedEvent;
        await using (var context = CreateContext(databaseName))
        {
            loadedEvent = await context.Events
                .Include(e => e.Organizers)
                .Include(e => e.Attendees)
                .SingleAsync(e => e.Id == evt.Id);
        }

        // Assert
        Assert.Single(loadedEvent.Organizers);
        Assert.Equal(user.Id, loadedEvent.Organizers[0].ParticipantId);

        Assert.Single(loadedEvent.Attendees);
        Assert.Equal(user.Id, loadedEvent.Attendees[0].ParticipantId);
        Assert.Null(loadedEvent.Attendees[0].RegisteredAt);
        Assert.Equal(EventAttendeeStatus.Invited, loadedEvent.Attendees[0].Status);
    }

    private static ApplicationDbContext CreateContext(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }
}
