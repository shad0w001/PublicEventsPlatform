using Application.Abstractions.Search;
using Confluent.Kafka;
using Infrastructure.DomainEvents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Messaging;

internal sealed class IndexerKafkaConsumer(
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaOptions> options,
    ILogger<IndexerKafkaConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        var kafkaOptions = options.Value;
        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = kafkaOptions.BootstrapServers,
            GroupId = kafkaOptions.IndexerConsumerGroupId,
            ClientId = $"{kafkaOptions.ClientId}-indexer",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
        consumer.Subscribe(IndexerConsumerTopics.IndexerTopics);

        logger.LogInformation(
            "Indexer Kafka consumer started (group {ConsumerGroupId}, topics: {Topics})",
            kafkaOptions.IndexerConsumerGroupId,
            string.Join(", ", IndexerConsumerTopics.IndexerTopics));

        var consumeTimeout = TimeSpan.FromSeconds(1);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var consumeResult = consumer.Consume(consumeTimeout);
                if (consumeResult is null)
                {
                    continue;
                }

                await ProcessMessageAsync(consumeResult, stoppingToken);
                consumer.Commit(consumeResult);
            }
            catch (ConsumeException ex) when (ex.Error.Code == ErrorCode.UnknownTopicOrPart)
            {
                logger.LogWarning(
                    "Indexer Kafka topics not available yet; retrying in 5 seconds");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (ConsumeException ex)
            {
                logger.LogError(ex, "Kafka consume error in indexer consumer");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Indexer consumer iteration failed");
            }
        }
    }

    private async Task ProcessMessageAsync(
        ConsumeResult<string, string> consumeResult,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(consumeResult.Message.Key, out var messageId))
        {
            logger.LogWarning(
                "Skipping indexer message on topic {Topic}: invalid message key {Key}",
                consumeResult.Topic,
                consumeResult.Message.Key);
            return;
        }

        var consumerName = GetConsumerName(consumeResult.Topic);
        if (consumerName is null)
        {
            logger.LogWarning(
                "Skipping indexer message {MessageId}: unmapped topic {Topic}",
                messageId,
                consumeResult.Topic);
            return;
        }

        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        var idempotency = scope.ServiceProvider.GetRequiredService<ConsumerIdempotencyService>();

        if (await idempotency.HasBeenProcessedAsync(consumerName, messageId, cancellationToken))
        {
            logger.LogDebug(
                "Skipping already processed indexer message {MessageId} ({ConsumerName})",
                messageId,
                consumerName);
            return;
        }

        await DispatchAsync(
            scope.ServiceProvider,
            consumeResult.Topic,
            consumeResult.Message.Value,
            cancellationToken);

        await idempotency.MarkProcessedAsync(consumerName, messageId, cancellationToken);

        logger.LogInformation(
            "Processed indexer message {MessageId} on topic {Topic}",
            messageId,
            consumeResult.Topic);
    }

    private static async Task DispatchAsync(
        IServiceProvider serviceProvider,
        string topic,
        string payload,
        CancellationToken cancellationToken)
    {
        switch (topic)
        {
            case IndexerConsumerTopics.EventPublished:
                await serviceProvider
                    .GetRequiredService<IEventPublishedEmbeddingStubHandler>()
                    .HandleAsync(
                        DomainEventPayloadDeserializer.DeserializeEventPublished(payload),
                        cancellationToken);
                break;
            case IndexerConsumerTopics.EventUpdated:
                await serviceProvider
                    .GetRequiredService<IEventUpdatedEmbeddingStubHandler>()
                    .HandleAsync(
                        DomainEventPayloadDeserializer.DeserializeEventUpdated(payload),
                        cancellationToken);
                break;
            default:
                throw new InvalidOperationException($"Unhandled indexer topic: {topic}");
        }
    }

    private static string? GetConsumerName(string topic) => topic switch
    {
        IndexerConsumerTopics.EventPublished => IndexerConsumerNames.EventPublished,
        IndexerConsumerTopics.EventUpdated => IndexerConsumerNames.EventUpdated,
        _ => null
    };
}
