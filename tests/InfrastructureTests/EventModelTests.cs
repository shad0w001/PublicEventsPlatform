using Domain.Events;
using Domain.Events.EventLocations;
using Domain.Events.Services;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureTests;

public class EventModelTests
{
    [Fact]
    public void EventModel_Should_ExposePhase3Columns_When_ApplicationDbContextModelIsBuilt()
    {
        // Arrange
        using var context = CreateContext();

        // Act
        var entityType = context.Model.FindEntityType(typeof(Event));

        // Assert
        Assert.NotNull(entityType);
        Assert.NotNull(entityType.FindProperty(nameof(Event.Tier)));
        Assert.NotNull(entityType.FindProperty(nameof(Event.TimeZoneId)));
        Assert.NotNull(entityType.FindProperty(nameof(Event.AdmissionType)));
        Assert.NotNull(entityType.FindProperty(nameof(Event.PublishedAt)));
        Assert.NotNull(entityType.FindProperty(nameof(Event.DeletedAt)));
        Assert.NotNull(entityType.FindProperty(nameof(Event.CreatedByUserId)));
    }

    [Fact]
    public void EventModel_Should_HaveStatusAndPublishedAtIndexes_When_ApplicationDbContextModelIsBuilt()
    {
        // Arrange
        using var context = CreateContext();

        // Act
        var entityType = context.Model.FindEntityType(typeof(Event));
        var statusIndex = entityType?.GetIndexes()
            .SingleOrDefault(i => i.Properties.Any(p => p.Name == nameof(Event.Status)));
        var publishedAtIndex = entityType?.GetIndexes()
            .SingleOrDefault(i => i.Properties.Any(p => p.Name == nameof(Event.PublishedAt)));

        // Assert
        Assert.NotNull(entityType);
        Assert.NotNull(statusIndex);
        Assert.NotNull(publishedAtIndex);
    }

    [Fact]
    public void EventOrganizerModel_Should_NotContainShadowEventId1_When_ApplicationDbContextModelIsBuilt()
    {
        // Arrange
        using var context = CreateContext();

        // Act
        var entityType = context.Model.FindEntityType(typeof(EventOrganizer));
        var shadowProperty = entityType?.FindProperty("EventId1");

        // Assert
        Assert.NotNull(entityType);
        Assert.Null(shadowProperty);
    }

    [Fact]
    public async Task Event_Should_PersistDraftWithOrganizerAndLocations_When_CreatedViaEventService()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var hostParticipantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var createResult = EventService.Create(EventTier.Small, hostParticipantId);
        var @event = createResult.Value.Event;
        var organizer = createResult.Value.Organizer;

        EventService.Update(
            @event,
            new EventUpdatePatch
            {
                Title = "Persisted Event",
                Locations =
                [
                    new EventLocation
                    {
                        Name = "Hall",
                        Date = new DateTime(2026, 8, 1, 18, 0, 0, DateTimeKind.Utc),
                        Kind = EventLocationKind.Physical,
                        Address = "1 Test St",
                        City = "Sofia"
                    }
                ]
            },
            actingUserId: Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));

        // Act
        await using (var context = CreateContext(databaseName))
        {
            context.Events.Add(@event);
            context.EventOrganizers.Add(organizer);
            await context.SaveChangesAsync();
        }

        Event loadedEvent;
        await using (var context = CreateContext(databaseName))
        {
            loadedEvent = await context.Events
                .Include(e => e.Organizers)
                .Include(e => e.Locations)
                .SingleAsync(e => e.Id == @event.Id);
        }

        // Assert
        Assert.Equal("Persisted Event", loadedEvent.Title);
        Assert.Equal(EventTier.Small, loadedEvent.Tier);
        Assert.Equal(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), loadedEvent.CreatedByUserId);
        Assert.Single(loadedEvent.Organizers);
        Assert.Equal(hostParticipantId, loadedEvent.Organizers[0].ParticipantId);
        Assert.Single(loadedEvent.Locations);
        Assert.Equal("Hall", loadedEvent.Locations[0].Name);
    }

    [Fact]
    public async Task EventCategories_Should_PersistHierarchy_When_SeededManually()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();

        // Act
        await using (var context = CreateContext(databaseName))
        {
            var parent = new EventCategory { Name = "Music" };
            var child = new EventCategory { Name = "Live Music", ParentCategoryId = parent.Id };
            context.EventCategories.Add(parent);
            context.EventCategories.Add(child);
            await context.SaveChangesAsync();
        }

        List<EventCategory> categories;
        await using (var context = CreateContext(databaseName))
        {
            categories = await context.EventCategories
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .ToListAsync();
        }

        // Assert
        Assert.Equal(2, categories.Count);
        var parentCategory = categories.Single(c => c.Name == "Music");
        var childCategory = categories.Single(c => c.Name == "Live Music");
        Assert.Null(parentCategory.ParentCategoryId);
        Assert.Equal(parentCategory.Id, childCategory.ParentCategoryId);
    }

    private static ApplicationDbContext CreateContext(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }
}
