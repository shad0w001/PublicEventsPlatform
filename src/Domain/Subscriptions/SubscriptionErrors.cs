using SharedKernel;

namespace Domain.Subscriptions;

public static class SubscriptionErrors
{
    public static readonly Error CityRequired = Error.Validation(
        "Subscriptions.CityRequired",
        "City is required for a city subscription");

    public static readonly Error CategoryRequired = Error.Validation(
        "Subscriptions.CategoryRequired",
        "Category is required for a category subscription");

    public static readonly Error CategoryNotFound = Error.Validation(
        "Subscriptions.CategoryNotFound",
        "The selected category was not found");

    public static readonly Error InvalidKindPayload = Error.Validation(
        "Subscriptions.InvalidKindPayload",
        "Unexpected fields were provided for the subscription kind");

    public static Error CityTooLong(int maxLength) => Error.Validation(
        "Subscriptions.CityTooLong",
        $"City must not exceed {maxLength} characters");

    public static readonly Error Duplicate = Error.Conflict(
        "Subscriptions.Duplicate",
        "This subscription already exists");

    public static readonly Error MaxLimitReached = Error.Conflict(
        "Subscriptions.MaxLimitReached",
        "The maximum number of subscriptions has been reached");

    public static Error NotFound(Guid subscriptionId) => Error.NotFound(
        "Subscriptions.NotFound",
        $"The subscription with the Id = '{subscriptionId}' was not found");

    public static readonly Error NotOwned = Error.Forbidden(
        "Subscriptions.NotOwned",
        "You are not authorized to modify this subscription");
}
