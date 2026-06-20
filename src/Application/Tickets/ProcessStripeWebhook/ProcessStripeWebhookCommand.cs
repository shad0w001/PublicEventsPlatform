using Application.Abstractions.Messaging;

namespace Application.Tickets.ProcessStripeWebhook;

public sealed record ProcessStripeWebhookCommand(
    string Json,
    string StripeSignatureHeader) : ICommand;
