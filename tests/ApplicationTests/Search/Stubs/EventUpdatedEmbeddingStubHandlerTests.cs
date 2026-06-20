using Application.Search.Stubs;
using ApplicationTests.Notifications.Handlers;
using Domain.Events;
using Domain.Events.EventLocations;
using Domain.Events.Events;
using Domain.Events.Services;
using Domain.Users;
using Domain.Users.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace ApplicationTests.Search.Stubs;

public class EventUpdatedEmbeddingStubHandlerTests
{
    [Fact]
    public async Task HandleAsync_Should_CompleteWithoutThrow_When_PublishedEventExists()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var host = CreateUser("auth0|index-host", "index-host@example.com");
        var eventId = await SeedEventAsync(databaseName, host, EventStatus.Published);
        var handler = CreateHandler(databaseName);

        // Act
        var act = () => handler.HandleAsync(new EventUpdated(eventId), CancellationToken.None);

        // Assert
        await act();
    }

    [Fact]
    public async Task HandleAsync_Should_CompleteWithoutThrow_When_DraftEventExists()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var host = CreateUser("auth0|index-draft-host", "index-draft-host@example.com");
        var eventId = await SeedEventAsync(databaseName, host, EventStatus.Draft);
        var handler = CreateHandler(databaseName);

        // Act
        var act = () => handler.HandleAsync(new EventUpdated(eventId), CancellationToken.None);

        // Assert
        await act();
    }

    [Fact]
    public async Task HandleAsync_Should_CompleteWithoutThrow_When_CancelledEventExists()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var host = CreateUser("auth0|index-cancel-host", "index-cancel-host@example.com");
        var eventId = await SeedEventAsync(databaseName, host, EventStatus.Cancelled);
        var handler = CreateHandler(databaseName);

        // Act
        var act = () => handler.HandleAsync(new EventUpdated(eventId), CancellationToken.None);

        // Assert
        await act();
    }

    [Fact]
    public async Task HandleAsync_Should_CompleteWithoutThrow_When_EventIsSoftDeleted()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var host = CreateUser("auth0|index-deleted-host", "index-deleted-host@example.com");
        var eventId = await SeedSoftDeletedDraftEventAsync(databaseName, host);
        var handler = CreateHandler(databaseName);

        // Act
        var act = () => handler.HandleAsync(new EventUpdated(eventId), CancellationToken.None);

        // Assert
        await act();
    }

    [Fact]
    public async Task HandleAsync_Should_CompleteWithoutThrow_When_EventDoesNotExist()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var handler = CreateHandler(databaseName);

        // Act
        var act = () => handler.HandleAsync(new EventUpdated(Guid.NewGuid()), CancellationToken.None);

        // Assert
        await act();
    }

    private static EventUpdatedEmbeddingStubHandler CreateHandler(string databaseName)
    {
        var context = NotificationHandlerTestSupport.CreateContext(databaseName);
        return new EventUpdatedEmbeddingStubHandler(
            context,
            NullLogger<EventUpdatedEmbeddingStubHandler>.Instance);
    }

    private static async Task<Guid> SeedEventAsync(
        string databaseName,
        User hostUser,
        EventStatus targetStatus)
    {
        var createResult = EventService.Create(EventTier.Small, "Indexed Event", hostUser.Id);
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

    private static async Task<Guid> SeedSoftDeletedDraftEventAsync(string databaseName, User hostUser)
    {
        var createResult = EventService.Create(EventTier.Small, "Deleted Event", hostUser.Id);
        var @event = createResult.Value.Event;
        EventService.SoftDelete(@event);

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
