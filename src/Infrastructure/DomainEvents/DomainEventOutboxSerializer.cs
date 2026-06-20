using System.Text.Json;
using SharedKernel;

namespace Infrastructure.DomainEvents;

internal static class DomainEventOutboxSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string Serialize(IDomainEvent domainEvent) =>
        JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), SerializerOptions);
}
