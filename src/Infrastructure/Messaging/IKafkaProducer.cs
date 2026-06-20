namespace Infrastructure.Messaging;

internal interface IKafkaProducer
{
    Task ProduceAsync(
        string topic,
        Guid messageId,
        string payload,
        CancellationToken cancellationToken);
}
