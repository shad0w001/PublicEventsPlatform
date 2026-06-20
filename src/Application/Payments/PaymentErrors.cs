using SharedKernel;

namespace Application.Payments;

public static class PaymentErrors
{
    public static readonly Error StripeNotConfigured = Error.Problem(
        "Payments.StripeNotConfigured",
        "Stripe secret key is not configured");

    public static readonly Error CheckoutSessionCreationFailed = Error.Problem(
        "Payments.CheckoutSessionCreationFailed",
        "Failed to create Stripe Checkout session");

    public static readonly Error WebhookSecretNotConfigured = Error.Problem(
        "Payments.WebhookSecretNotConfigured",
        "Stripe webhook secret is not configured");

    public static readonly Error InvalidWebhookSignature = Error.Validation(
        "Payments.InvalidWebhookSignature",
        "Stripe webhook signature verification failed");

    public static readonly Error InvalidWebhookPayload = Error.Validation(
        "Payments.InvalidWebhookPayload",
        "Stripe webhook payload is invalid or missing required metadata");
}
