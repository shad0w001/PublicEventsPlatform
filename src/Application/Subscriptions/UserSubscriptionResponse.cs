using Domain.Subscriptions;

namespace Application.Subscriptions;

public sealed record UserSubscriptionResponse(
    Guid Id,
    SubscriptionKind Kind,
    string? City,
    Guid? CategoryId,
    string? CategoryName,
    DateTime CreatedAt);
