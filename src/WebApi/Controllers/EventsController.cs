using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Events;
using Application.Events.CreateEvent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using WebApi.Extensions;

namespace WebApi.Controllers;

[ApiController]
[Route("api/events")]
[SwaggerTag("Events")]
public sealed class EventsController(
    ICommandHandler<CreateEventCommand, EventSummaryResponse> createEventHandler) : ControllerBase
{
    [Authorize]
    [HttpPost]
    [SwaggerOperation(
        Summary = "Create a draft event",
        Description = """
            Wizard screen 1: creates a draft event with tier, title, and host (self or group).
            Requires verified email. Host defaults to the caller when hostId is omitted.
            CreatedByUserId is set on the first PATCH (screen 2+). Returns 201 with event summary.
            """)]
    [SwaggerResponse(StatusCodes.Status201Created, "Draft event created", typeof(EventSummaryResponse))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Not authenticated", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Email not verified or insufficient host permissions", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Host participant not found", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Validation failed (e.g. invalid title)", typeof(ProblemDetails))]
    public async Task<IActionResult> Create(
        [FromBody] CreateEventCommand command,
        CancellationToken cancellationToken)
    {
        var result = await createEventHandler.Handle(command, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Created($"/api/events/{result.Value.Id}", result.Value);
    }
}
