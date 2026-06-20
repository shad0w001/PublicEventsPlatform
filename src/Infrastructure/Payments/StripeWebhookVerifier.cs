using Application.Abstractions.Payments;
using Application.Payments;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharedKernel;
using Stripe;

namespace Infrastructure.Payments;

internal sealed class StripeWebhookVerifier(
    IOptions<StripeOptions> options,
    ILogger<StripeWebhookVerifier> logger) : IStripeWebhookVerifier
{
    private static readonly HashSet<string> HandledEventTypes =
    [
        "checkout.session.completed",
        "checkout.session.expired"
    ];

    public Task<Result<StripeWebhookEvent>> VerifyAsync(
        string json,
        string signatureHeader,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Value.WebhookSecret))
        {
            return Task.FromResult(Result.Failure<StripeWebhookEvent>(
                PaymentErrors.WebhookSecretNotConfigured));
        }

        if (string.IsNullOrWhiteSpace(signatureHeader))
        {
            return Task.FromResult(Result.Failure<StripeWebhookEvent>(
                PaymentErrors.InvalidWebhookSignature));
        }

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(
                json,
                signatureHeader,
                options.Value.WebhookSecret);
        }
        catch (StripeException ex)
        {
            logger.LogWarning(ex, "Stripe webhook signature verification failed");
            return Task.FromResult(Result.Failure<StripeWebhookEvent>(
                PaymentErrors.InvalidWebhookSignature));
        }

        if (!HandledEventTypes.Contains(stripeEvent.Type))
        {
            logger.LogDebug("Ignoring unhandled Stripe webhook event type {EventType}", stripeEvent.Type);
            return Task.FromResult(Result.Success(new StripeWebhookEvent(
                StripeWebhookEventType.Ignored,
                string.Empty,
                Guid.Empty,
                Guid.Empty,
                Guid.Empty,
                null)));
        }

        if (stripeEvent.Data.Object is not Stripe.Checkout.Session session)
        {
            logger.LogWarning(
                "Stripe webhook event {EventType} did not contain a checkout session object",
                stripeEvent.Type);
            return Task.FromResult(Result.Failure<StripeWebhookEvent>(
                PaymentErrors.InvalidWebhookPayload));
        }

        if (string.IsNullOrWhiteSpace(session.Id))
        {
            return Task.FromResult(Result.Failure<StripeWebhookEvent>(
                PaymentErrors.InvalidWebhookPayload));
        }

        if (!TryParseMetadata(session.Metadata, out var orderId, out var eventId, out var ticketTypeId))
        {
            logger.LogWarning(
                "Stripe checkout session {SessionId} is missing required metadata",
                session.Id);
            return Task.FromResult(Result.Failure<StripeWebhookEvent>(
                PaymentErrors.InvalidWebhookPayload));
        }

        var eventType = stripeEvent.Type switch
        {
            "checkout.session.completed" => StripeWebhookEventType.CheckoutSessionCompleted,
            "checkout.session.expired" => StripeWebhookEventType.CheckoutSessionExpired,
            _ => StripeWebhookEventType.Ignored
        };

        var paymentIntentId = session.PaymentIntentId
            ?? (session.PaymentIntent as PaymentIntent)?.Id;

        return Task.FromResult(Result.Success(new StripeWebhookEvent(
            eventType,
            session.Id,
            orderId,
            eventId,
            ticketTypeId,
            paymentIntentId)));
    }

    private static bool TryParseMetadata(
        IReadOnlyDictionary<string, string>? metadata,
        out Guid orderId,
        out Guid eventId,
        out Guid ticketTypeId)
    {
        orderId = Guid.Empty;
        eventId = Guid.Empty;
        ticketTypeId = Guid.Empty;

        if (metadata is null)
        {
            return false;
        }

        return metadata.TryGetValue("orderId", out var orderIdValue)
            && Guid.TryParse(orderIdValue, out orderId)
            && metadata.TryGetValue("eventId", out var eventIdValue)
            && Guid.TryParse(eventIdValue, out eventId)
            && metadata.TryGetValue("ticketTypeId", out var ticketTypeIdValue)
            && Guid.TryParse(ticketTypeIdValue, out ticketTypeId);
    }
}
