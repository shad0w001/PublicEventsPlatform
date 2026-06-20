using Application.Abstractions.Messaging;
using Application.Tickets;
using Application.Tickets.CreateEventTicketType;
using Application.Tickets.DeleteEventTicketType;
using Application.Tickets.UpdateEventTicketType;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using WebApi.Extensions;

namespace WebApi.Controllers;

[ApiController]
[Route("api/events/{eventId:guid}/ticket-types")]
[SwaggerTag("EventTicketTypes")]
public sealed class EventTicketTypesController(
    ICommandHandler<CreateEventTicketTypeCommand, TicketTypeResponse> createHandler,
    ICommandHandler<UpdateEventTicketTypeCommand, TicketTypeResponse> updateHandler,
    ICommandHandler<DeleteEventTicketTypeCommand> deleteHandler) : ControllerBase
{
    [Authorize]
    [HttpPost]
    [SwaggerOperation(
        Summary = "Create a ticket type on a paid event",
        Description = """
            Wizard screen 6: adds a sellable ticket listing to a paid-admission event (draft or published).
            Requires verified email and event edit permission (same as PATCH /api/events/{eventId}).
            Paid admission only; price must be greater than zero; capacity must be greater than zero.
            Returns 201 with TicketTypeResponse. Errors: Tickets.PaidAdmissionRequired,
            Events.CannotModifyCancelled (409), Events.InsufficientPermissions (403).
            """)]
    [SwaggerResponse(StatusCodes.Status201Created, "Ticket type created", typeof(TicketTypeResponse))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Not authenticated", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Email not verified or insufficient permissions", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Event not found", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Validation failed", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status409Conflict, "Event cancelled", typeof(ProblemDetails))]
    public async Task<IActionResult> Create(
        Guid eventId,
        [FromBody] CreateEventTicketTypeRequest body,
        CancellationToken cancellationToken)
    {
        var command = new CreateEventTicketTypeCommand(
            eventId,
            body.Name,
            body.Description,
            body.PriceCents,
            body.Capacity);

        var result = await createHandler.Handle(command, cancellationToken);

        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Created($"/api/events/{eventId}/ticket-types/{result.Value.Id}", result.Value);
    }

    [Authorize]
    [HttpPatch("{ticketTypeId:guid}")]
    [SwaggerOperation(
        Summary = "Update a ticket type on an event",
        Description = """
            Updates name, description, price (before first sale), and/or capacity on a ticket type.
            Requires verified email and event edit permission.
            Price is immutable after SoldQuantity > 0. Capacity must stay at or above sold plus reserved.
            Returns 200 with TicketTypeResponse. Errors: Tickets.TicketTypeNotFound,
            Tickets.PriceImmutableAfterSales (409), Tickets.CapacityTooLow,
            Events.CannotModifyCancelled (409), Events.InsufficientPermissions (403).
            """)]
    [SwaggerResponse(StatusCodes.Status200OK, "Ticket type updated", typeof(TicketTypeResponse))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Not authenticated", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Email not verified or insufficient permissions", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Event or ticket type not found", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Validation failed", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status409Conflict, "Price immutable or event cancelled", typeof(ProblemDetails))]
    public async Task<IActionResult> Update(
        Guid eventId,
        Guid ticketTypeId,
        [FromBody] UpdateEventTicketTypeRequest body,
        CancellationToken cancellationToken)
    {
        var command = new UpdateEventTicketTypeCommand(
            eventId,
            ticketTypeId,
            body.Name,
            body.Description,
            body.PriceCents,
            body.Capacity);

        var result = await updateHandler.Handle(command, cancellationToken);
        return result.ToActionResult();
    }

    [Authorize]
    [HttpDelete("{ticketTypeId:guid}")]
    [SwaggerOperation(
        Summary = "Delete a ticket type from an event",
        Description = """
            Removes a ticket type when it has no sales and no reserved inventory.
            Requires verified email and event edit permission.
            Returns 204 on success. Errors: Tickets.TicketTypeNotFound,
            Tickets.CannotDeleteWithSales (409), Tickets.CannotDeleteWithReserved (409),
            Events.CannotModifyCancelled (409), Events.InsufficientPermissions (403).
            """)]
    [SwaggerResponse(StatusCodes.Status204NoContent, "Ticket type deleted")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Not authenticated", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Email not verified or insufficient permissions", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Event or ticket type not found", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status409Conflict, "Has sales/reservations or event cancelled", typeof(ProblemDetails))]
    public async Task<IActionResult> Delete(
        Guid eventId,
        Guid ticketTypeId,
        CancellationToken cancellationToken)
    {
        var command = new DeleteEventTicketTypeCommand(eventId, ticketTypeId);
        var result = await deleteHandler.Handle(command, cancellationToken);
        return result.ToActionResult();
    }
}
