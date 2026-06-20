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
}
