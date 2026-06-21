using Application.Abstractions.Messaging;

namespace Application.Subscriptions.ListMySubscriptions;

public sealed record ListMySubscriptionsQuery : IQuery<IReadOnlyList<UserSubscriptionResponse>>;
