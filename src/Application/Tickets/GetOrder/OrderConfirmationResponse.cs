using Domain.Events;
using Domain.Tickets;

namespace Application.Tickets.GetOrder;

public sealed record OrderConfirmationEventResponse(
    Guid EventId,
    string Title,
    EventStatus Status,
    DateTime StartTime,
    DateTime EndTime,
    string TimeZoneId,
    string? BannerImageUrl,
    Guid HostParticipantId,
    bool HostIsGroup,
    string HostDisplayName);

public sealed record OrderConfirmationResponse(
    Guid OrderId,
    OrderStatus Status,
    int Quantity,
    string TicketTypeName,
    int PriceCents,
    DateTime PurchasedAt,
    bool RefundPending,
    OrderConfirmationEventResponse Event,
    IReadOnlyList<Guid> TicketIds);
