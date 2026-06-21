using Domain.Subscriptions;

namespace Application.Subscriptions;

internal static class SubscriptionMapping
{
    public static UserSubscriptionResponse Map(
        UserSubscription subscription,
        string? categoryName) =>
        new(
            subscription.Id,
            subscription.Kind,
            subscription.City,
            subscription.CategoryId,
            categoryName,
            subscription.CreatedAt);
}
