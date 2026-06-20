using Application.Abstractions.Messaging;
using Application.Tickets.ProcessStripeWebhook;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using WebApi.Extensions;

namespace WebApi.Controllers;

[ApiController]
[Route("api/webhooks/stripe")]
[SwaggerTag("StripeWebhooks")]
public sealed class StripeWebhooksController(
    ICommandHandler<ProcessStripeWebhookCommand> handler) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost]
    [SwaggerOperation(
        Summary = "Receive Stripe webhook events",
        Description = """
            Anonymous endpoint for Stripe webhook delivery. Verifies the Stripe-Signature header
            using Payments:Stripe:WebhookSecret (from stripe listen in local dev).
            Handles checkout.session.completed (fulfill order, issue tickets, upsert paid attendance)
            and checkout.session.expired (release reserved inventory). Other event types are acknowledged
            with 200 and ignored. Idempotent on retries. Requires stripe listen forwarding to this URL
            during local development.
            """)]
    [SwaggerResponse(StatusCodes.Status200OK, "Webhook accepted")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Invalid signature or payload", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Referenced order not found", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "Processing failed", typeof(ProblemDetails))]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var json = await reader.ReadToEndAsync(cancellationToken);

        var signatureHeader = Request.Headers["Stripe-Signature"].ToString();
        var result = await handler.Handle(
            new ProcessStripeWebhookCommand(json, signatureHeader),
            cancellationToken);

        if (result.IsSuccess)
        {
            return Ok();
        }

        return result.ToActionResult();
    }
}
