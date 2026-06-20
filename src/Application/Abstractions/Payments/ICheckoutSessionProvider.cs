using SharedKernel;

namespace Application.Abstractions.Payments;

public sealed record CheckoutSessionRequest(
    Guid OrderId,
    Guid EventId,
    Guid TicketTypeId,
    string TicketTypeName,
    int PriceCents,
    int Quantity,
    string CustomerEmail,
    string SuccessUrl,
    string CancelUrl,
    DateTime ExpiresAtUtc);

public sealed record CheckoutSessionResult(
    string SessionId,
    string CheckoutUrl,
    string? PaymentIntentId);

public interface ICheckoutSessionProvider
{
    Task<Result<CheckoutSessionResult>> CreateAsync(
        CheckoutSessionRequest request,
        CancellationToken cancellationToken);
}
