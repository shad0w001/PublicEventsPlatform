using Application.Abstractions.Groups;
using Confluent.Kafka;
using Infrastructure.DomainEvents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Messaging;

internal sealed class CascadeKafkaConsumer(
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaOptions> options,
    ILogger<CascadeKafkaConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        var kafkaOptions = options.Value;
        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = kafkaOptions.BootstrapServers,
            GroupId = kafkaOptions.CascadeConsumerGroupId,
            ClientId = $"{kafkaOptions.ClientId}-cascade",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
        consumer.Subscribe(CascadeConsumerTopics.CascadeTopics);

        logger.LogInformation(
            "Cascade Kafka consumer started (group {ConsumerGroupId}, topics: {Topics})",
            kafkaOptions.CascadeConsumerGroupId,
            string.Join(", ", CascadeConsumerTopics.CascadeTopics));

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
                    "Cascade Kafka topics not available yet; retrying in 5 seconds");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (ConsumeException ex)
            {
                logger.LogError(ex, "Kafka consume error in cascade consumer");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Cascade consumer iteration failed");
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
                "Skipping cascade message on topic {Topic}: invalid message key {Key}",
                consumeResult.Topic,
                consumeResult.Message.Key);
            return;
        }

        var consumerName = GetConsumerName(consumeResult.Topic);
        if (consumerName is null)
        {
            logger.LogWarning(
                "Skipping cascade message {MessageId}: unmapped topic {Topic}",
                messageId,
                consumeResult.Topic);
            return;
        }

        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        var idempotency = scope.ServiceProvider.GetRequiredService<ConsumerIdempotencyService>();

        if (await idempotency.HasBeenProcessedAsync(consumerName, messageId, cancellationToken))
        {
            logger.LogDebug(
                "Skipping already processed cascade message {MessageId} ({ConsumerName})",
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
            "Processed cascade message {MessageId} on topic {Topic}",
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
            case CascadeConsumerTopics.GroupSoftDeleted:
                await serviceProvider
                    .GetRequiredService<IGroupSoftDeletedCascadeHandler>()
                    .HandleAsync(
                        DomainEventPayloadDeserializer.DeserializeGroupSoftDeleted(payload),
                        cancellationToken);
                break;
            default:
                throw new InvalidOperationException($"Unhandled cascade topic: {topic}");
        }
    }

    private static string? GetConsumerName(string topic) => topic switch
    {
        CascadeConsumerTopics.GroupSoftDeleted => CascadeConsumerNames.GroupSoftDeleted,
        _ => null
    };
}
