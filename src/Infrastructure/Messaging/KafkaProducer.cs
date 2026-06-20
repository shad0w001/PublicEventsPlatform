using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Messaging;

internal sealed class KafkaProducer : IKafkaProducer, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaProducer> _logger;

    public KafkaProducer(IOptions<KafkaOptions> options, ILogger<KafkaProducer> logger)
    {
        _logger = logger;

        var config = new ProducerConfig
        {
            BootstrapServers = options.Value.BootstrapServers,
            ClientId = options.Value.ClientId,
            Acks = Acks.All
        };

        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    public async Task ProduceAsync(
        string topic,
        Guid messageId,
        string payload,
        CancellationToken cancellationToken)
    {
        var message = new Message<string, string>
        {
            Key = messageId.ToString(),
            Value = payload
        };

        try
        {
            var result = await _producer.ProduceAsync(topic, message, cancellationToken);
            _logger.LogDebug(
                "Published outbox message {MessageId} to {Topic} at {Offset}",
                messageId,
                topic,
                result.Offset);
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogError(
                ex,
                "Failed to publish outbox message {MessageId} to {Topic}",
                messageId,
                topic);
            throw;
        }
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
    }
}
