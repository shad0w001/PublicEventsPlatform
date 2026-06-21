using Application.Abstractions.Messaging;
using Domain.Subscriptions;

namespace Application.Subscriptions.CreateSubscription;

public sealed record CreateSubscriptionCommand(
    SubscriptionKind Kind,
    string? City,
    Guid? CategoryId) : ICommand<UserSubscriptionResponse>;
