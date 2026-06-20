namespace Application.Payments;

public sealed class StripeOptions
{
    public const string SectionName = "Payments:Stripe";

    public string SecretKey { get; init; } = string.Empty;

    public string WebhookSecret { get; init; } = string.Empty;

    public string SuccessUrlBase { get; init; } = string.Empty;

    public string CancelUrlBase { get; init; } = string.Empty;

    public int PendingOrderTtlMinutes { get; init; } = 30;
}
