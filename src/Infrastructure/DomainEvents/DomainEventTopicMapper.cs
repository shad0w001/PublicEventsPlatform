using Domain.Events.Events;
using Domain.Groups.Events;
using Domain.Tickets.Events;
using SharedKernel;

namespace Infrastructure.DomainEvents;

internal static class DomainEventTopicMapper
{
    private static readonly Dictionary<Type, string> Topics = new()
    {
        [typeof(TicketPurchaseCompleted)] = "domain.ticket-purchase-completed",
        [typeof(EventRsvpStatusChanged)] = "domain.event-rsvp-status-changed",
        [typeof(EventCancelled)] = "domain.event-cancelled",
        [typeof(GroupSoftDeleted)] = "domain.group-soft-deleted",
        [typeof(GroupJoinApplicationSubmitted)] = "domain.group-join-application-submitted",
        [typeof(GroupJoinApplicationApproved)] = "domain.group-join-application-approved",
        [typeof(GroupJoinApplicationRejected)] = "domain.group-join-application-rejected",
        [typeof(EventPublished)] = "domain.event-published",
        [typeof(EventUpdated)] = "domain.event-updated"
    };

    public static bool TryGetTopic(IDomainEvent domainEvent, out string topic) =>
        Topics.TryGetValue(domainEvent.GetType(), out topic!);

    public static IReadOnlyCollection<string> GetAllTopics() => Topics.Values;
}
