using System.Text.Json;
using Domain.Events.Events;
using Domain.Groups.Events;
using Domain.Tickets.Events;

namespace Infrastructure.DomainEvents;

internal static class DomainEventPayloadDeserializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static TicketPurchaseCompleted DeserializeTicketPurchaseCompleted(string payload) =>
        JsonSerializer.Deserialize<TicketPurchaseCompleted>(payload, SerializerOptions)
        ?? throw new InvalidOperationException("Failed to deserialize TicketPurchaseCompleted payload.");

    public static EventRsvpStatusChanged DeserializeEventRsvpStatusChanged(string payload) =>
        JsonSerializer.Deserialize<EventRsvpStatusChanged>(payload, SerializerOptions)
        ?? throw new InvalidOperationException("Failed to deserialize EventRsvpStatusChanged payload.");

    public static EventCancelled DeserializeEventCancelled(string payload) =>
        JsonSerializer.Deserialize<EventCancelled>(payload, SerializerOptions)
        ?? throw new InvalidOperationException("Failed to deserialize EventCancelled payload.");

    public static GroupJoinApplicationSubmitted DeserializeGroupJoinApplicationSubmitted(string payload) =>
        JsonSerializer.Deserialize<GroupJoinApplicationSubmitted>(payload, SerializerOptions)
        ?? throw new InvalidOperationException("Failed to deserialize GroupJoinApplicationSubmitted payload.");

    public static GroupJoinApplicationApproved DeserializeGroupJoinApplicationApproved(string payload) =>
        JsonSerializer.Deserialize<GroupJoinApplicationApproved>(payload, SerializerOptions)
        ?? throw new InvalidOperationException("Failed to deserialize GroupJoinApplicationApproved payload.");

    public static GroupJoinApplicationRejected DeserializeGroupJoinApplicationRejected(string payload) =>
        JsonSerializer.Deserialize<GroupJoinApplicationRejected>(payload, SerializerOptions)
        ?? throw new InvalidOperationException("Failed to deserialize GroupJoinApplicationRejected payload.");

    public static GroupSoftDeleted DeserializeGroupSoftDeleted(string payload) =>
        JsonSerializer.Deserialize<GroupSoftDeleted>(payload, SerializerOptions)
        ?? throw new InvalidOperationException("Failed to deserialize GroupSoftDeleted payload.");
}
