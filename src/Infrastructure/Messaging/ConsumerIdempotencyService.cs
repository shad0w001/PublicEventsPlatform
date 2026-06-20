using Infrastructure.Database;
using Infrastructure.DomainEvents;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Messaging;

internal sealed class ConsumerIdempotencyService(ApplicationDbContext context)
{
    public async Task<bool> HasBeenProcessedAsync(
        string consumerName,
        Guid messageId,
        CancellationToken cancellationToken) =>
        await context.ProcessedMessages
            .AsNoTracking()
            .AnyAsync(
                m => m.ConsumerName == consumerName && m.MessageId == messageId,
                cancellationToken);

    public async Task MarkProcessedAsync(
        string consumerName,
        Guid messageId,
        CancellationToken cancellationToken)
    {
        if (await HasBeenProcessedAsync(consumerName, messageId, cancellationToken))
        {
            return;
        }

        context.ProcessedMessages.Add(new ProcessedMessage
        {
            ConsumerName = consumerName,
            MessageId = messageId,
            ProcessedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync(cancellationToken);
    }
}
