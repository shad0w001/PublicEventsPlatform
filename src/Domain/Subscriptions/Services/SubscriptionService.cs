using Domain.Events;
using SharedKernel;

namespace Domain.Subscriptions.Services;

public static class SubscriptionService
{
    public static Result<UserSubscription> Create(
        Guid userId,
        SubscriptionKind kind,
        string? city,
        Guid? categoryId,
        bool categoryExists,
        IReadOnlyList<UserSubscription> existingSubscriptions)
    {
        if (existingSubscriptions.Count >= SubscriptionConstants.MaxSubscriptionsPerUser)
        {
            return Result.Failure<UserSubscription>(SubscriptionErrors.MaxLimitReached);
        }

        var payloadResult = ValidateKindPayload(kind, city, categoryId);
        if (payloadResult.IsFailure)
        {
            return Result.Failure<UserSubscription>(payloadResult.Error);
        }

        string? normalizedCity = null;
        if (kind == SubscriptionKind.City)
        {
            normalizedCity = EventVenueRules.NormalizePlaceComponent(city);
            if (normalizedCity is null)
            {
                return Result.Failure<UserSubscription>(SubscriptionErrors.CityRequired);
            }

            if (normalizedCity.Length > SubscriptionConstants.CityMaxLength)
            {
                return Result.Failure<UserSubscription>(
                    SubscriptionErrors.CityTooLong(SubscriptionConstants.CityMaxLength));
            }
        }

        if (kind == SubscriptionKind.Category)
        {
            if (categoryId is null || categoryId == Guid.Empty)
            {
                return Result.Failure<UserSubscription>(SubscriptionErrors.CategoryRequired);
            }

            if (!categoryExists)
            {
                return Result.Failure<UserSubscription>(SubscriptionErrors.CategoryNotFound);
            }
        }

        if (IsDuplicate(kind, normalizedCity, categoryId, existingSubscriptions))
        {
            return Result.Failure<UserSubscription>(SubscriptionErrors.Duplicate);
        }

        var subscription = UserSubscription.Create(userId, kind, normalizedCity, categoryId);
        return subscription;
    }

    public static Result Delete(UserSubscription subscription, Guid userId)
    {
        if (subscription.UserId != userId)
        {
            return Result.Failure(SubscriptionErrors.NotOwned);
        }

        return Result.Success();
    }

    private static Result ValidateKindPayload(
        SubscriptionKind kind,
        string? city,
        Guid? categoryId)
    {
        var hasCity = !string.IsNullOrWhiteSpace(city);
        var hasCategory = categoryId is not null && categoryId != Guid.Empty;

        return kind switch
        {
            SubscriptionKind.City when hasCategory => Result.Failure(SubscriptionErrors.InvalidKindPayload),
            SubscriptionKind.Category when hasCity => Result.Failure(SubscriptionErrors.InvalidKindPayload),
            SubscriptionKind.Online when hasCity || hasCategory => Result.Failure(SubscriptionErrors.InvalidKindPayload),
            _ => Result.Success()
        };
    }

    private static bool IsDuplicate(
        SubscriptionKind kind,
        string? normalizedCity,
        Guid? categoryId,
        IReadOnlyList<UserSubscription> existingSubscriptions) =>
        kind switch
        {
            SubscriptionKind.City => existingSubscriptions.Any(existing =>
                existing.Kind == SubscriptionKind.City &&
                string.Equals(existing.City, normalizedCity, StringComparison.Ordinal)),
            SubscriptionKind.Category => existingSubscriptions.Any(existing =>
                existing.Kind == SubscriptionKind.Category &&
                existing.CategoryId == categoryId),
            SubscriptionKind.Online => existingSubscriptions.Any(existing =>
                existing.Kind == SubscriptionKind.Online),
            _ => false
        };
}
