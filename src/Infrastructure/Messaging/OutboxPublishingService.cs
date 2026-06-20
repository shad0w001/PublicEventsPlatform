using Infrastructure.Database;
using Infrastructure.DomainEvents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Messaging;

internal sealed class OutboxPublishingService(
    IServiceScopeFactory scopeFactory,
    IKafkaProducer kafkaProducer,
    IOptions<KafkaOptions> options,
    ILogger<OutboxPublishingService> logger)
{
    public async Task PublishPendingAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var batchSize = options.Value.OutboxBatchSize > 0
            ? options.Value.OutboxBatchSize
            : 20;

        var pendingMessages = await context.OutboxMessages
            .Where(m => m.PublishedAt == null)
            .OrderBy(m => m.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (pendingMessages.Count == 0)
        {
            return;
        }

        foreach (var message in pendingMessages)
        {
            try
            {
                await kafkaProducer.ProduceAsync(
                    message.Topic,
                    message.Id,
                    message.Payload,
                    cancellationToken);

                message.PublishedAt = DateTime.UtcNow;
                message.Error = null;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Failed to publish outbox message {MessageId} ({EventType})",
                    message.Id,
                    message.EventType);

                message.Error = ex.Message.Length > 2000
                    ? ex.Message[..2000]
                    : ex.Message;
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
