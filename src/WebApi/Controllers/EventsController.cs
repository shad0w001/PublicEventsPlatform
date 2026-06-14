using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Events;
using Application.Events.CreateEvent;
using Application.Events.PublishEvent;
using Application.Events.UpdateEvent;
using Domain.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using WebApi.Extensions;

namespace WebApi.Controllers;

[ApiController]
[Route("api/events")]
[SwaggerTag("Events")]
public sealed class EventsController(
    ICommandHandler<CreateEventCommand, EventSummaryResponse> createEventHandler,
    ICommandHandler<UpdateEventCommand, EventDetailResponse> updateEventHandler,
    ICommandHandler<PublishEventCommand, EventDetailResponse> publishEventHandler) : ControllerBase
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

    [Authorize]
    [HttpPatch("{eventId:guid}")]
    [SwaggerOperation(
        Summary = "Update an event (partial)",
        Description = """
            Partial update for the creation wizard (screens 2–5) and post-publish edits.
            Requires verified email and edit permission (host, creator, or group Organizer+).
            Omitted fields are unchanged; locations[] replaces the full list when sent.
            CreatedByUserId is set on the first PATCH. Draft non-editors receive 403.
            Returns 200 with full event detail.
            """)]
    [SwaggerResponse(StatusCodes.Status200OK, "Event updated", typeof(EventDetailResponse))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Not authenticated", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Email not verified or insufficient permissions", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Event, category, or host not found", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "No fields to update or validation failed", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status409Conflict, "Cancelled event or tier downgrade blocked", typeof(ProblemDetails))]
    public async Task<IActionResult> Update(
        Guid eventId,
        [FromBody] UpdateEventRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateEventCommand(
            eventId,
            request.Title,
            request.Description,
            request.CategoryId,
            request.BannerImageUrl,
            request.Tier,
            request.Locations,
            request.StartTime,
            request.EndTime,
            request.TimeZoneId,
            request.AdmissionType);

        var result = await updateEventHandler.Handle(command, cancellationToken);
        return result.ToActionResult();
    }

    [Authorize]
    [HttpPost("{eventId:guid}/publish")]
    [SwaggerOperation(
        Summary = "Publish a draft event",
        Description = """
            Wizard review step: validates publish minimums and makes the event public.
            Requires verified email and edit permission (host, creator, or group Organizer+).
            Rate limit: 6 publishes per host participant per rolling 7 days (by PublishedAt).
            Paid admission is allowed without ticket types until the tickets phase.
            Returns 200 with full published event detail.
            """)]
    [SwaggerResponse(StatusCodes.Status200OK, "Event published", typeof(EventDetailResponse))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Not authenticated", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Email not verified or insufficient permissions", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Event not found or deleted", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Publish validation failed (incomplete draft)", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status409Conflict, "Already published, rate limit exceeded, or cancelled", typeof(ProblemDetails))]
    public async Task<IActionResult> Publish(Guid eventId, CancellationToken cancellationToken)
    {
        var result = await publishEventHandler.Handle(new PublishEventCommand(eventId), cancellationToken);
        return result.ToActionResult();
    }
}

public sealed record UpdateEventRequest(
    string? Title = null,
    string? Description = null,
    Guid? CategoryId = null,
    string? BannerImageUrl = null,
    EventTier? Tier = null,
    IReadOnlyList<EventLocationResponse>? Locations = null,
    DateTime? StartTime = null,
    DateTime? EndTime = null,
    string? TimeZoneId = null,
    AdmissionType? AdmissionType = null);
