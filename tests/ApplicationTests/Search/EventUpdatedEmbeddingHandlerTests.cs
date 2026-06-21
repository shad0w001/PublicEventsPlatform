using Application.Search;
using ApplicationTests.Notifications.Handlers;
using Domain.Events;
using Domain.Events.EventLocations;
using Domain.Events.Events;
using Domain.Events.Services;
using Domain.Users;
using Domain.Users.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace ApplicationTests.Search;

using ApplicationTests.Events;

public class EventUpdatedEmbeddingHandlerTests
{
    [Fact]
    public async Task HandleAsync_Should_CallUpsertAsync_When_PublishedEventExists()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var host = CreateUser("auth0|updated-host", "updated-host@example.com");
        var eventId = await SeedEventAsync(databaseName, host, EventStatus.Published);
        var recordingIndexService = new RecordingEmbeddingIndexService();
        var handler = CreateUpdatedHandler(databaseName, recordingIndexService);

        // Act
        await handler.HandleAsync(new EventUpdated(eventId), CancellationToken.None);

        // Assert
        Assert.Equal([eventId], recordingIndexService.UpsertedEventIds);
    }

    [Fact]
    public async Task HandleAsync_Should_NotCallUpsertAsync_When_DraftEventExists()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var host = CreateUser("auth0|updated-draft-host", "updated-draft-host@example.com");
        var eventId = await SeedEventAsync(databaseName, host, EventStatus.Draft);
        var recordingIndexService = new RecordingEmbeddingIndexService();
        var handler = CreateUpdatedHandler(databaseName, recordingIndexService);

        // Act
        await handler.HandleAsync(new EventUpdated(eventId), CancellationToken.None);

        // Assert
        Assert.Empty(recordingIndexService.UpsertedEventIds);
    }

    [Fact]
    public async Task HandleAsync_Should_NotCallUpsertAsync_When_CancelledEventExists()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var host = CreateUser("auth0|updated-cancel-host", "updated-cancel-host@example.com");
        var eventId = await SeedEventAsync(databaseName, host, EventStatus.Cancelled);
        var recordingIndexService = new RecordingEmbeddingIndexService();
        var handler = CreateUpdatedHandler(databaseName, recordingIndexService);

        // Act
        await handler.HandleAsync(new EventUpdated(eventId), CancellationToken.None);

        // Assert
        Assert.Empty(recordingIndexService.UpsertedEventIds);
    }

    private static EventUpdatedEmbeddingHandler CreateUpdatedHandler(
        string databaseName,
        RecordingEmbeddingIndexService recordingIndexService)
    {
        var context = NotificationHandlerTestSupport.CreateContext(databaseName);
        return new EventUpdatedEmbeddingHandler(
            context,
            recordingIndexService,
            NullLogger<EventUpdatedEmbeddingHandler>.Instance);
    }

    private static async Task<Guid> SeedEventAsync(
        string databaseName,
        User hostUser,
        EventStatus targetStatus)
    {
        var createResult = EventService.Create(EventTier.Small, "Indexed Event", hostUser.Id, EventTestConstants.DefaultBannerUrl);
        var @event = createResult.Value.Event;
        @event.Description = "Description";
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

        if (targetStatus is EventStatus.Published or EventStatus.Cancelled)
        {
            EventService.Publish(@event, categoryExists: true, recentPublishCount: 0, maxPublishesPerWeek: 6);
        }

        if (targetStatus == EventStatus.Cancelled)
        {
            EventService.Cancel(@event);
        }

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
