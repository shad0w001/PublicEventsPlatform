using Application.Abstractions.Messaging;
using Application.Tickets.CreateEventOrder;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using WebApi.Extensions;

namespace WebApi.Controllers;

[ApiController]
[Route("api/events/{eventId:guid}/orders")]
[SwaggerTag("EventOrders")]
public sealed class EventOrdersController(
    ICommandHandler<CreateEventOrderCommand, CreateEventOrderResponse> createHandler) : ControllerBase
{
    [Authorize]
    [HttpPost]
    [SwaggerOperation(
        Summary = "Create a ticket order and Stripe Checkout session",
        Description = """
            Starts checkout for one ticket type on a published paid event.
            Requires verified email. Buyer is the authenticated user unless participantId
            is a group the caller may buy for (Organizer, Administrator, or Owner).
            Reserves inventory synchronously (DB row lock) and returns a Stripe Checkout URL.
            Ticket fulfillment happens via Stripe webhook after payment—success redirect alone
            does not issue tickets.
            Optional successUrl/cancelUrl override Payments:Stripe:SuccessUrlBase/CancelUrlBase.
            Errors: Tickets.EventNotPublished, Tickets.EventCancelled (409),
            Tickets.PaidAdmissionRequired, Tickets.InvalidQuantity, Tickets.InsufficientInventory (409),
            Tickets.TicketTypeNotFound, Tickets.TicketTypeEventMismatch,
            Tickets.InsufficientPurchasePermissions (403), Payments.StripeNotConfigured.
            """)]
    [SwaggerResponse(StatusCodes.Status201Created, "Order created with checkout URL", typeof(CreateEventOrderResponse))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Not authenticated", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Email not verified or insufficient purchase permissions", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Event or ticket type not found", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Validation failed", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status409Conflict, "Insufficient inventory or event cancelled", typeof(ProblemDetails))]
    public async Task<IActionResult> Create(
        Guid eventId,
        [FromBody] CreateEventOrderRequest body,
        CancellationToken cancellationToken)
    {
        var command = new CreateEventOrderCommand(
            eventId,
            body.TicketTypeId,
            body.Quantity,
            body.ParticipantId,
            body.SuccessUrl,
            body.CancelUrl);

        var result = await createHandler.Handle(command, cancellationToken);

        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Created($"/api/events/{eventId}/orders/{result.Value.OrderId}", result.Value);
    }
}
