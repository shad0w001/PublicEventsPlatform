using Application.Abstractions.Data;
using Application.Abstractions.Payments;
using Application.Payments;
using Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharedKernel;
using Stripe;
using Stripe.Checkout;

namespace Infrastructure.Payments;

internal sealed class StripeCheckoutSessionProvider(
    IOptions<StripeOptions> options,
    ILogger<StripeCheckoutSessionProvider> logger) : ICheckoutSessionProvider
{
    public async Task<Result<CheckoutSessionResult>> CreateAsync(
        CheckoutSessionRequest request,
        CancellationToken cancellationToken)
    {
        var stripeOptions = options.Value;
        if (string.IsNullOrWhiteSpace(stripeOptions.SecretKey))
        {
            return Result.Failure<CheckoutSessionResult>(PaymentErrors.StripeNotConfigured);
        }

        StripeConfiguration.ApiKey = stripeOptions.SecretKey;

        var sessionOptions = new SessionCreateOptions
        {
            Mode = "payment",
            CustomerEmail = request.CustomerEmail,
            SuccessUrl = request.SuccessUrl,
            CancelUrl = request.CancelUrl,
            ExpiresAt = request.ExpiresAtUtc,
            Metadata = new Dictionary<string, string>
            {
                ["orderId"] = request.OrderId.ToString(),
                ["eventId"] = request.EventId.ToString(),
                ["ticketTypeId"] = request.TicketTypeId.ToString()
            },
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Quantity = request.Quantity,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = "eur",
                        UnitAmount = request.PriceCents,
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = request.TicketTypeName
                        }
                    }
                }
            ]
        };

        try
        {
            var service = new SessionService();
            var session = await service.CreateAsync(sessionOptions, cancellationToken: cancellationToken);

            if (string.IsNullOrWhiteSpace(session.Id) || string.IsNullOrWhiteSpace(session.Url))
            {
                return Result.Failure<CheckoutSessionResult>(PaymentErrors.CheckoutSessionCreationFailed);
            }

            var paymentIntentId = session.PaymentIntentId
                ?? (session.PaymentIntent as PaymentIntent)?.Id;

            return new CheckoutSessionResult(
                session.Id,
                session.Url,
                paymentIntentId);
        }
        catch (StripeException ex)
        {
            logger.LogWarning(ex, "Stripe Checkout session creation failed for order {OrderId}", request.OrderId);
            return Result.Failure<CheckoutSessionResult>(PaymentErrors.CheckoutSessionCreationFailed);
        }
    }
}
