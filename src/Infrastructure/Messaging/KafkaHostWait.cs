using Confluent.Kafka;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Messaging;

public static class KafkaHostWait
{
    public static async Task WaitForBrokerAsync(IConfiguration configuration, int maxAttempts = 15)
    {
        var bootstrapServers = configuration[$"{KafkaOptions.SectionName}:BootstrapServers"];
        if (string.IsNullOrWhiteSpace(bootstrapServers))
        {
            return;
        }

        var config = new AdminClientConfig
        {
            BootstrapServers = bootstrapServers
        };

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var admin = new AdminClientBuilder(config).Build();
                admin.GetMetadata(TimeSpan.FromSeconds(3));
                return;
            }
            catch (KafkaException)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }
    }
}
