using Application.Search;
using Application.Search.Services;
using ApplicationTests.Notifications.Handlers;
using Domain.Events;
using Domain.Events.EventLocations;
using Domain.Events.Events;
using Domain.Events.Services;
using Domain.Users;
using Domain.Users.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace ApplicationTests.Search;

public class EventPublishedEmbeddingHandlerTests
{
    [Fact]
    public async Task HandleAsync_Should_CallUpsertAsync_When_EventPublished()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var hostId = Guid.NewGuid();
        var recordingIndexService = new RecordingEmbeddingIndexService();
        var handler = new EventPublishedEmbeddingHandler(recordingIndexService);

        // Act
        await handler.HandleAsync(new EventPublished(eventId, hostId), CancellationToken.None);

        // Assert
        Assert.Equal([eventId], recordingIndexService.UpsertedEventIds);
    }
}

public class EventEmbeddingIndexServiceDocumentTests
{
    [Fact]
    public async Task BuildSearchDocumentAsync_Should_IncludeEventFields_When_PublishedEventExists()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var host = CreateUser("auth0|doc-host", "doc-host@example.com");
        var eventId = await SeedPublishedEventAsync(databaseName, host, "Jazz Night", "Live music downtown");
        var indexService = CreateIndexService(databaseName);

        // Act
        var document = await indexService.BuildSearchDocumentAsync(eventId, CancellationToken.None);

        // Assert
        Assert.NotNull(document);
        Assert.Contains("Jazz Night", document);
        Assert.Contains("Live music downtown", document);
        Assert.Contains("Sofia", document);
    }

    private static EventEmbeddingIndexService CreateIndexService(string databaseName)
    {
        var context = NotificationHandlerTestSupport.CreateContext(databaseName);
        return new EventEmbeddingIndexService(
            context,
            new FakeEmbeddingGenerator(),
            TimeProvider.System,
            NullLogger<EventEmbeddingIndexService>.Instance);
    }

    private static async Task<Guid> SeedPublishedEventAsync(
        string databaseName,
        User hostUser,
        string title,
        string description)
    {
        var createResult = EventService.Create(EventTier.Small, title, hostUser.Id);
        var @event = createResult.Value.Event;
        @event.Description = description;
        @event.CategoryId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        @event.StartTime = new DateTime(2026, 8, 1, 18, 0, 0, DateTimeKind.Utc);
        @event.EndTime = new DateTime(2026, 8, 1, 22, 0, 0, DateTimeKind.Utc);
        @event.TimeZoneId = "Europe/Sofia";
        @event.AdmissionType = AdmissionType.Free;
        @event.Locations.Add(new EventLocation
        {
            Name = "Hall",
            Kind = EventLocationKind.Physical,
            Address = "123 Main St",
            City = "Sofia"
        });
        EventService.Publish(@event, categoryExists: true, recentPublishCount: 0, maxPublishesPerWeek: 6);

        await using var context = NotificationHandlerTestSupport.CreateContext(databaseName);
        context.Users.Add(hostUser);
        context.Events.Add(@event);
        context.EventOrganizers.Add(createResult.Value.Organizer);
        await context.SaveChangesAsync();

        return @event.Id;
    }

    private static User CreateUser(string subject, string email) =>
        UserService.ProvisionFromExternalIdentity(
            subject,
            email,
            emailVerified: true,
            profilePictureUrl: null,
            NotificationHandlerTestSupport.DefaultAvatarUrl,
            ServiceRole.User);
}
