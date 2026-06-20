using Microsoft.Extensions.Options;

namespace Application.Notifications;

public sealed class NotificationLinkBuilder(IOptions<NotificationsOptions> options)
{
    private readonly string _baseUrl = options.Value.PublicAppBaseUrl.TrimEnd('/');

    public string Event(Guid eventId) => $"{_baseUrl}/events/{eventId}";

    public string Ticket(Guid ticketId) => $"{_baseUrl}/tickets/{ticketId}";

    public string Group(Guid groupId) => $"{_baseUrl}/groups/{groupId}";

    public string Order(Guid orderId) => $"{_baseUrl}/orders/{orderId}";
}
