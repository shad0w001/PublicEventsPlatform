using Application.Abstractions.Notifications;
using Confluent.Kafka;
using Infrastructure.DomainEvents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Messaging;

internal sealed class NotificationKafkaConsumer(
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaOptions> options,
    ILogger<NotificationKafkaConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // BackgroundService runs ExecuteAsync synchronously until the first await.
        // Confluent's Consume() blocks; yield so host startup can finish (Kestrel bind).
        await Task.Yield();

        var kafkaOptions = options.Value;
        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = kafkaOptions.BootstrapServers,
            GroupId = kafkaOptions.ConsumerGroupId,
            ClientId = $"{kafkaOptions.ClientId}-notifications",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
        consumer.Subscribe(NotificationConsumerTopics.NotificationTopics);

        logger.LogInformation(
            "Notification Kafka consumer started (group {ConsumerGroupId}, topics: {Topics})",
            kafkaOptions.ConsumerGroupId,
            string.Join(", ", NotificationConsumerTopics.NotificationTopics));

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
                    "Notification Kafka topics not available yet; retrying in 5 seconds");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (ConsumeException ex)
            {
                logger.LogError(ex, "Kafka consume error in notification consumer");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Notification consumer iteration failed");
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
                "Skipping notification message on topic {Topic}: invalid message key {Key}",
                consumeResult.Topic,
                consumeResult.Message.Key);
            return;
        }

        var consumerName = GetConsumerName(consumeResult.Topic);
        if (consumerName is null)
        {
            logger.LogWarning(
                "Skipping notification message {MessageId}: unmapped topic {Topic}",
                messageId,
                consumeResult.Topic);
            return;
        }

        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        var idempotency = scope.ServiceProvider.GetRequiredService<ConsumerIdempotencyService>();

        if (await idempotency.HasBeenProcessedAsync(consumerName, messageId, cancellationToken))
        {
            logger.LogDebug(
                "Skipping already processed notification {MessageId} ({ConsumerName})",
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
            "Processed notification {MessageId} on topic {Topic}",
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
            case NotificationConsumerTopics.TicketPurchaseCompleted:
                await serviceProvider
                    .GetRequiredService<ITicketPurchaseCompletedEmailHandler>()
                    .HandleAsync(DomainEventPayloadDeserializer.DeserializeTicketPurchaseCompleted(payload), cancellationToken);
                break;
            case NotificationConsumerTopics.EventRsvpStatusChanged:
                await serviceProvider
                    .GetRequiredService<IEventRsvpStatusChangedEmailHandler>()
                    .HandleAsync(DomainEventPayloadDeserializer.DeserializeEventRsvpStatusChanged(payload), cancellationToken);
                break;
            case NotificationConsumerTopics.EventCancelled:
                await serviceProvider
                    .GetRequiredService<IEventCancelledEmailHandler>()
                    .HandleAsync(DomainEventPayloadDeserializer.DeserializeEventCancelled(payload), cancellationToken);
                break;
            case NotificationConsumerTopics.GroupJoinApplicationSubmitted:
                await serviceProvider
                    .GetRequiredService<IGroupJoinApplicationSubmittedEmailHandler>()
                    .HandleAsync(DomainEventPayloadDeserializer.DeserializeGroupJoinApplicationSubmitted(payload), cancellationToken);
                break;
            case NotificationConsumerTopics.GroupJoinApplicationApproved:
                await serviceProvider
                    .GetRequiredService<IGroupJoinApplicationApprovedEmailHandler>()
                    .HandleAsync(DomainEventPayloadDeserializer.DeserializeGroupJoinApplicationApproved(payload), cancellationToken);
                break;
            case NotificationConsumerTopics.GroupJoinApplicationRejected:
                await serviceProvider
                    .GetRequiredService<IGroupJoinApplicationRejectedEmailHandler>()
                    .HandleAsync(DomainEventPayloadDeserializer.DeserializeGroupJoinApplicationRejected(payload), cancellationToken);
                break;
            case NotificationConsumerTopics.GroupVerificationApplicationApproved:
                await serviceProvider
                    .GetRequiredService<IGroupVerificationApplicationApprovedEmailHandler>()
                    .HandleAsync(DomainEventPayloadDeserializer.DeserializeGroupVerificationApplicationApproved(payload), cancellationToken);
                break;
            case NotificationConsumerTopics.GroupVerificationApplicationRejected:
                await serviceProvider
                    .GetRequiredService<IGroupVerificationApplicationRejectedEmailHandler>()
                    .HandleAsync(DomainEventPayloadDeserializer.DeserializeGroupVerificationApplicationRejected(payload), cancellationToken);
                break;
            default:
                throw new InvalidOperationException($"Unhandled notification topic: {topic}");
        }
    }

    private static string? GetConsumerName(string topic) => topic switch
    {
        NotificationConsumerTopics.TicketPurchaseCompleted => NotificationConsumerNames.TicketPurchaseCompleted,
        NotificationConsumerTopics.EventRsvpStatusChanged => NotificationConsumerNames.EventRsvpStatusChanged,
        NotificationConsumerTopics.EventCancelled => NotificationConsumerNames.EventCancelled,
        NotificationConsumerTopics.GroupJoinApplicationSubmitted => NotificationConsumerNames.GroupJoinApplicationSubmitted,
        NotificationConsumerTopics.GroupJoinApplicationApproved => NotificationConsumerNames.GroupJoinApplicationApproved,
        NotificationConsumerTopics.GroupJoinApplicationRejected => NotificationConsumerNames.GroupJoinApplicationRejected,
        NotificationConsumerTopics.GroupVerificationApplicationApproved => NotificationConsumerNames.GroupVerificationApplicationApproved,
        NotificationConsumerTopics.GroupVerificationApplicationRejected => NotificationConsumerNames.GroupVerificationApplicationRejected,
        _ => null
    };
}
