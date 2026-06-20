using SharedKernel;

namespace Application.Abstractions.Payments;

public enum StripeWebhookEventType
{
    Ignored,
    CheckoutSessionCompleted,
    CheckoutSessionExpired
}

public sealed record StripeWebhookEvent(
    StripeWebhookEventType EventType,
    string SessionId,
    Guid OrderId,
    Guid EventId,
    Guid TicketTypeId,
    string? PaymentIntentId);

public interface IStripeWebhookVerifier
{
    Task<Result<StripeWebhookEvent>> VerifyAsync(
        string json,
        string signatureHeader,
        CancellationToken cancellationToken);
}
