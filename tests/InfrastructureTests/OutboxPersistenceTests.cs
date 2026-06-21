using Domain.Events;
using Domain.Events.Events;
using Domain.Events.Services;
using Infrastructure.Database;
using Infrastructure.DomainEvents;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureTests;

public class OutboxPersistenceTests
{
    [Fact]
    public async Task ApplicationDbContext_Should_PersistOutboxMessage_When_DomainEventIsRaisedOnSave()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        Guid eventId;

        await using (var context = CreateContext(databaseName))
        {
            var createResult = EventService.Create(EventTier.Small, "Outbox test", Guid.CreateVersion7(), "/images/default-event-banner.png");
            Assert.True(createResult.IsSuccess);

            var @event = createResult.Value.Event;
            eventId = @event.Id;
            @event.Raise(new EventCancelled(eventId));
            context.Events.Add(@event);
            context.EventOrganizers.Add(createResult.Value.Organizer);

            // Act
            await context.SaveChangesAsync();
        }

        // Assert
        await using var verifyContext = CreateContext(databaseName);
        var outboxMessage = await verifyContext.OutboxMessages.SingleAsync();

        Assert.Equal(nameof(EventCancelled), outboxMessage.EventType);
        Assert.Equal("domain.event-cancelled", outboxMessage.Topic);
        Assert.Contains(eventId.ToString(), outboxMessage.Payload, StringComparison.OrdinalIgnoreCase);
        Assert.Null(outboxMessage.PublishedAt);
    }

    [Fact]
    public async Task ApplicationDbContext_Should_NotPersistOutboxMessage_When_DomainEventIsUnmapped()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        await using (var context = CreateContext(databaseName))
        {
            var createResult = EventService.Create(EventTier.Small, "Unmapped outbox", Guid.CreateVersion7(), "/images/default-event-banner.png");
            Assert.True(createResult.IsSuccess);

            context.Events.Add(createResult.Value.Event);
            context.EventOrganizers.Add(createResult.Value.Organizer);

            // Act — EventCreated is raised but not mapped to a Kafka topic
            await context.SaveChangesAsync();
        }

        // Assert
        await using var verifyContext = CreateContext(databaseName);
        Assert.Empty(await verifyContext.OutboxMessages.ToListAsync());
    }

    [Fact]
    public async Task ApplicationDbContext_Should_ClearEntityDomainEvents_When_OutboxMessageIsPersisted()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();

        await using (var context = CreateContext(databaseName))
        {
            var createResult = EventService.Create(EventTier.Small, "Clear events", Guid.CreateVersion7(), "/images/default-event-banner.png");
            Assert.True(createResult.IsSuccess);

            var @event = createResult.Value.Event;
            @event.Raise(new EventCancelled(@event.Id));
            context.Events.Add(@event);
            context.EventOrganizers.Add(createResult.Value.Organizer);

            // Act
            await context.SaveChangesAsync();

            // Assert
            Assert.Empty(@event.DomainEvents);
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
