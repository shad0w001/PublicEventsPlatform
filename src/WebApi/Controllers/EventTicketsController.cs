using Application.Abstractions.Messaging;
using Application.Tickets.ValidateEventTicket;
using Domain.Tickets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using WebApi.Extensions;

namespace WebApi.Controllers;

[ApiController]
[Route("api/events/{eventId:guid}/tickets")]
[SwaggerTag("EventTickets")]
public sealed class EventTicketsController(
    ICommandHandler<ValidateEventTicketCommand, ValidateEventTicketResponse> validateHandler) : ControllerBase
{
    [Authorize]
    [HttpPost("validate")]
    [SwaggerOperation(
        Summary = "Validate a ticket for door check-in",
        Description = """
            Door check-in endpoint for event organizers.
            Requires verified email and Organizer+ edit access on the event (same permission as event PATCH).
            Event must be published (not draft) and paid-admission only.

            Accepts a QR payload (ticket UUID) or a manual entry code (8-char alphanumeric, case-insensitive).
            Always returns 200 with a status field — the SPA uses the status to show the success or error screen.

            Status values:
            - Valid: first successful check-in; audit row written.
            - AlreadyUsed: ticket was already checked in; audit row written.
            - Invalid: code not found, wrong event, outside validation window (15 min before StartTime through EndTime), or cancelled event. No audit row written when code is unknown.

            HTTP errors (ProblemDetails) are only returned for access/state failures:
            - 401: not authenticated.
            - 403: email not verified or not event host Organizer+.
            - 404: event not found or deleted.
            - 409: event is draft or admission type is not paid.
            """)]
    [SwaggerResponse(StatusCodes.Status200OK, "Validation outcome", typeof(ValidateEventTicketResponse))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Not authenticated", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Not event host Organizer+", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Event not found", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status409Conflict, "Event is draft or not a paid event", typeof(ProblemDetails))]
    public async Task<IActionResult> Validate(
        Guid eventId,
        [FromBody] ValidateEventTicketRequest body,
        CancellationToken cancellationToken)
    {
        var command = new ValidateEventTicketCommand(eventId, body.Code, body.Method);
        var result = await validateHandler.Handle(command, cancellationToken);
        return result.ToActionResult();
    }
}

public sealed record ValidateEventTicketRequest(string Code, TicketValidationMethod Method);
