using Application.Abstractions.Messaging;

namespace Application.Subscriptions.DeleteSubscription;

public sealed record DeleteSubscriptionCommand(Guid SubscriptionId) : ICommand;
