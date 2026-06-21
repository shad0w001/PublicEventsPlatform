using Domain.Users;
using SharedKernel;

namespace Domain.Subscriptions;

public sealed class UserSubscription : Entity
{
    public Guid UserId { get; internal set; }
    public SubscriptionKind Kind { get; internal set; }
    public string? City { get; internal set; }
    public Guid? CategoryId { get; internal set; }

    public User User { get; internal set; } = null!;

    internal static UserSubscription Create(
        Guid userId,
        SubscriptionKind kind,
        string? city,
        Guid? categoryId) =>
        new()
        {
            UserId = userId,
            Kind = kind,
            City = city,
            CategoryId = categoryId
        };
}
