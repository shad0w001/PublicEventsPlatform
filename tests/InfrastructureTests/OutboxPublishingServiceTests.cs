using Infrastructure.Database;
using Infrastructure.DomainEvents;
using Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace InfrastructureTests;

public class OutboxPublishingServiceTests
{
    [Fact]
    public async Task OutboxPublishingService_Should_SetPublishedAt_When_KafkaProduceSucceeds()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var messageId = Guid.CreateVersion7();
        var fakeProducer = new FakeKafkaProducer(shouldSucceed: true);

        await using (var context = CreateContext(databaseName))
        {
            context.OutboxMessages.Add(new OutboxMessage
            {
                Id = messageId,
                EventType = "EventCancelled",
                Topic = "domain.event-cancelled",
                Payload = "{}",
                OccurredOnUtc = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }

        var service = CreateService(databaseName, fakeProducer);

        // Act
        await service.PublishPendingAsync(CancellationToken.None);

        // Assert
        await using var verifyContext = CreateContext(databaseName);
        var message = await verifyContext.OutboxMessages.SingleAsync(m => m.Id == messageId);
        Assert.NotNull(message.PublishedAt);
        Assert.Null(message.Error);
        Assert.Single(fakeProducer.ProducedMessages);
    }

    [Fact]
    public async Task OutboxPublishingService_Should_SetError_When_KafkaProduceFails()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var messageId = Guid.CreateVersion7();
        var fakeProducer = new FakeKafkaProducer(shouldSucceed: false);

        await using (var context = CreateContext(databaseName))
        {
            context.OutboxMessages.Add(new OutboxMessage
            {
                Id = messageId,
                EventType = "EventCancelled",
                Topic = "domain.event-cancelled",
                Payload = "{}",
                OccurredOnUtc = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }

        var service = CreateService(databaseName, fakeProducer);

        // Act
        await service.PublishPendingAsync(CancellationToken.None);

        // Assert
        await using var verifyContext = CreateContext(databaseName);
        var message = await verifyContext.OutboxMessages.SingleAsync(m => m.Id == messageId);
        Assert.Null(message.PublishedAt);
        Assert.NotNull(message.Error);
        Assert.Empty(fakeProducer.ProducedMessages);
    }

    private static OutboxPublishingService CreateService(
        string databaseName,
        IKafkaProducer kafkaProducer)
    {
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));
        services.AddScoped<OutboxPublishingService>();

        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        return new OutboxPublishingService(
            scopeFactory,
            kafkaProducer,
            Options.Create(new KafkaOptions()),
            NullLogger<OutboxPublishingService>.Instance);
    }

    private static ApplicationDbContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new ApplicationDbContext(options);
    }

    private sealed class FakeKafkaProducer(bool shouldSucceed) : IKafkaProducer
    {
        public List<(string Topic, Guid MessageId, string Payload)> ProducedMessages { get; } = [];

        public Task ProduceAsync(
            string topic,
            Guid messageId,
            string payload,
            CancellationToken cancellationToken)
        {
            if (!shouldSucceed)
            {
                throw new InvalidOperationException("Kafka produce failed.");
            }

            ProducedMessages.Add((topic, messageId, payload));
            return Task.CompletedTask;
        }
    }
}
