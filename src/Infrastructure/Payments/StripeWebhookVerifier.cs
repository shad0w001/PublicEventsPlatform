using Application.Abstractions.Payments;
using Application.Payments;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using SharedKernel;
using Stripe;
using Stripe.Checkout;

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

    public async Task<Result<StripeWebhookEvent>> VerifyAsync(
        string json,
        string signatureHeader,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Value.WebhookSecret))
        {
            return Result.Failure<StripeWebhookEvent>(PaymentErrors.WebhookSecretNotConfigured);
        }

        if (string.IsNullOrWhiteSpace(signatureHeader))
        {
            return Result.Failure<StripeWebhookEvent>(PaymentErrors.InvalidWebhookSignature);
        }

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(
                json,
                signatureHeader,
                options.Value.WebhookSecret,
                throwOnApiVersionMismatch: false);
        }
        catch (StripeException ex)
        {
            logger.LogWarning(
                ex,
                "Stripe webhook verification failed (signature or payload). Event API version from CLI may differ from Stripe.net — ensure WebhookSecret matches `stripe listen` output.");
            return Result.Failure<StripeWebhookEvent>(PaymentErrors.InvalidWebhookSignature);
        }

        if (!HandledEventTypes.Contains(stripeEvent.Type))
        {
            logger.LogDebug("Ignoring unhandled Stripe webhook event type {EventType}", stripeEvent.Type);
            return Result.Success(new StripeWebhookEvent(
                StripeWebhookEventType.Ignored,
                string.Empty,
                Guid.Empty,
                Guid.Empty,
                Guid.Empty,
                null));
        }

        var session = await ResolveCheckoutSessionAsync(stripeEvent, cancellationToken);
        if (session is null || string.IsNullOrWhiteSpace(session.Id))
        {
            logger.LogWarning(
                "Stripe webhook event {EventType} did not resolve to a checkout session",
                stripeEvent.Type);
            return Result.Failure<StripeWebhookEvent>(PaymentErrors.InvalidWebhookPayload);
        }

        if (!TryParseMetadata(session.Metadata, out var orderId, out var eventId, out var ticketTypeId))
        {
            logger.LogWarning(
                "Stripe checkout session {SessionId} is missing required metadata (orderId, eventId, ticketTypeId)",
                session.Id);
            return Result.Failure<StripeWebhookEvent>(PaymentErrors.InvalidWebhookPayload);
        }

        var eventType = stripeEvent.Type switch
        {
            "checkout.session.completed" => StripeWebhookEventType.CheckoutSessionCompleted,
            "checkout.session.expired" => StripeWebhookEventType.CheckoutSessionExpired,
            _ => StripeWebhookEventType.Ignored
        };

        var paymentIntentId = session.PaymentIntentId
            ?? (session.PaymentIntent as PaymentIntent)?.Id;

        return Result.Success(new StripeWebhookEvent(
            eventType,
            session.Id,
            orderId,
            eventId,
            ticketTypeId,
            paymentIntentId));
    }

    private async Task<Session?> ResolveCheckoutSessionAsync(
        Event stripeEvent,
        CancellationToken cancellationToken)
    {
        if (stripeEvent.Data.Object is Session session
            && TryParseMetadata(session.Metadata, out _, out _, out _))
        {
            return session;
        }

        var sessionId = ExtractSessionId(stripeEvent);
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return stripeEvent.Data.Object as Session;
        }

        if (string.IsNullOrWhiteSpace(options.Value.SecretKey))
        {
            logger.LogWarning(
                "Checkout session {SessionId} metadata missing in webhook payload and Stripe secret key is not configured for retrieval",
                sessionId);
            return stripeEvent.Data.Object as Session;
        }

        try
        {
            StripeConfiguration.ApiKey = options.Value.SecretKey;
            var service = new SessionService();
            return await service.GetAsync(sessionId, cancellationToken: cancellationToken);
        }
        catch (StripeException ex)
        {
            logger.LogWarning(ex, "Failed to retrieve Stripe checkout session {SessionId}", sessionId);
            return stripeEvent.Data.Object as Session;
        }
    }

    private static string? ExtractSessionId(Event stripeEvent)
    {
        if (stripeEvent.Data.Object is Session session && !string.IsNullOrWhiteSpace(session.Id))
        {
            return session.Id;
        }

        if (stripeEvent.Data.RawObject is JObject raw && raw.TryGetValue("id", out var idToken))
        {
            return idToken?.ToString();
        }

        return null;
    }

    internal static bool TryParseMetadata(
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
