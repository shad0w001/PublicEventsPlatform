using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Infrastructure.DomainEvents;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Messaging;

public static class KafkaTopicProvisioner
{
    public static async Task EnsureTopicsAsync(
        IConfiguration configuration,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        var bootstrapServers = configuration[$"{KafkaOptions.SectionName}:BootstrapServers"];
        if (string.IsNullOrWhiteSpace(bootstrapServers))
        {
            return;
        }

        var adminConfig = new AdminClientConfig
        {
            BootstrapServers = bootstrapServers
        };

        using var admin = new AdminClientBuilder(adminConfig).Build();
        var existingTopics = admin
            .GetMetadata(TimeSpan.FromSeconds(10))
            .Topics
            .Where(t => t.Error.Code == ErrorCode.NoError)
            .Select(t => t.Topic)
            .ToHashSet(StringComparer.Ordinal);

        var topicsToCreate = DomainEventTopicMapper.GetAllTopics()
            .Where(topic => !existingTopics.Contains(topic))
            .Select(topic => new TopicSpecification
            {
                Name = topic,
                NumPartitions = 1,
                ReplicationFactor = 1
            })
            .ToList();

        if (topicsToCreate.Count == 0)
        {
            return;
        }

        try
        {
            await admin.CreateTopicsAsync(topicsToCreate);
            logger?.LogInformation(
                "Created Kafka topics: {Topics}",
                string.Join(", ", topicsToCreate.Select(t => t.Name)));
        }
        catch (CreateTopicsException ex)
        {
            var unexpectedErrors = ex.Results
                .Where(result => result.Error.Code != ErrorCode.TopicAlreadyExists)
                .ToList();

            if (unexpectedErrors.Count > 0)
            {
                throw;
            }

            logger?.LogDebug("Kafka topics already existed during provisioning");
        }
    }
}
