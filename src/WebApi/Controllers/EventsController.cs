using Application.Abstractions.Authentication;
using Application.Abstractions.Media;
using Application.Abstractions.Messaging;
using Application.Events;
using Application.Events.CancelEvent;
using Application.Events.CreateEvent;
using Application.Events.DeleteEvent;
using Application.Events.GetEvent;
using Application.Events.ListMyEvents;
using Application.Events.PublishEvent;
using Application.Events.UpdateEvent;
using Application.Events.UploadEventBanner;
using Domain.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using SharedKernel;
using WebApi.Extensions;

namespace WebApi.Controllers;

[ApiController]
[Route("api/events")]
[SwaggerTag("Events")]
public sealed class EventsController(
    ICommandHandler<CreateEventCommand, EventSummaryResponse> createEventHandler,
    IQueryHandler<ListMyEventsQuery, IReadOnlyList<MyEventListItemResponse>> listMyEventsHandler,
    IQueryHandler<GetEventQuery, GetEventResponse> getEventHandler,
    ICommandHandler<UpdateEventCommand, EventDetailResponse> updateEventHandler,
    ICommandHandler<PublishEventCommand, EventDetailResponse> publishEventHandler,
    ICommandHandler<UploadEventBannerCommand, EventDetailResponse> uploadEventBannerHandler,
    ICommandHandler<CancelEventCommand> cancelEventHandler,
    ICommandHandler<DeleteEventCommand> deleteEventHandler) : ControllerBase
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
    [HttpGet("mine")]
    [SwaggerOperation(
        Summary = "List my manageable events",
        Description = """
            Returns non-deleted events the verified caller can edit: self-hosted or group-hosted with current Organizer+ membership.
            Includes Draft, Published, and Cancelled.
            Sorted by status (Draft, Published, Cancelled), then StartTime ascending, then CreatedAt descending.
            Attending/RSVP events are not included (Phase 5). Returns 200 with an empty list when none match.
            """)]
    [SwaggerResponse(StatusCodes.Status200OK, "Manageable events", typeof(IReadOnlyList<MyEventListItemResponse>))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Not authenticated", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Email not verified", typeof(ProblemDetails))]
    public async Task<IActionResult> ListMine(CancellationToken cancellationToken)
    {
        var result = await listMyEventsHandler.Handle(new ListMyEventsQuery(), cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("{eventId:guid}")]
    [SwaggerOperation(
        Summary = "Get an event (public page or editor view)",
        Description = """
            Anonymous access allowed for published and cancelled events (PublicEventResponse).
            Draft events return 404 unless the caller is an editor (EditDetail only).
            Eligible editors receive EditDetail only (EventDetailResponse), never both public and detail.
            CanEdit is false when the event is cancelled (read-only editor view; mutations return 409).
            Deleted events return 404. Draft startTime/endTime may be Unix epoch until wizard screen 3 (times) is saved.
            locations[].startsAt and endsAt are nullable (UTC). Null segment times mean the full event window
            (startTime–endTime) for display; clients derive locally—no server-side effective* fields.
            """)]
    [SwaggerResponse(StatusCodes.Status200OK, "Event view", typeof(GetEventResponse))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Event not found, deleted, or draft hidden", typeof(ProblemDetails))]
    public async Task<IActionResult> Get(Guid eventId, CancellationToken cancellationToken)
    {
        var result = await getEventHandler.Handle(new GetEventQuery(eventId), cancellationToken);
        return result.ToActionResult();
    }

    [Authorize]
    [HttpPatch("{eventId:guid}")]
    [SwaggerOperation(
        Summary = "Update an event (partial)",
        Description = """
            Partial update for the creation wizard (screens 2–5) and post-publish edits.
            Wizard: screen 2 = description/category; screen 3 = startTime, endTime, timeZoneId;
            screen 4 = locations[]; screen 5 = admissionType (see Phase 4 wizard in AGENTS.md).
            Requires verified email and edit permission (user host, user-hosted CreatedByUserId, or current group Organizer+).
            Omitted fields are unchanged; locations[] replaces the full list when sent.
            Location segments: startsAt/endsAt optional (UTC); both required if either is set; must fall within
            event startTime/endTime when event times are set. Physical: address OR city OR latitude+longitude;
            virtual: url. Patching event times re-validates existing segments (may 400 without resending locations).
            Published events: venue conflict check on locations[] or startTime/endTime PATCH (physical segments only).
            CreatedByUserId is set on the first PATCH. Draft non-editors receive 403.
            Returns 200 with full event detail.
            """)]
    [SwaggerResponse(StatusCodes.Status200OK, "Event updated", typeof(EventDetailResponse))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Not authenticated", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Email not verified or insufficient permissions", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Event, category, or host not found", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "No fields to update or validation failed (e.g. Events.SegmentTimesIncomplete, Events.InvalidSegmentTimeRange, Events.SegmentTimeOutOfBounds, Events.InvalidLocationSegment)", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status409Conflict, "Cancelled event, tier downgrade blocked, or Events.VenueConflict", typeof(ProblemDetails))]
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
    [HttpPost("{eventId:guid}/banner")]
    [Consumes("multipart/form-data")]
    [SwaggerOperation(
        Summary = "Upload event banner image",
        Description = """
            Uploads a banner image for a draft or published event. Requires verified email and edit permission.
            Accepts JPEG, PNG, or WebP up to profile limits; stored as WebP under /uploads/events/.
            Only updates bannerImageUrl; other event fields are unchanged. Optional during the creation wizard.
            Returns 200 with full event detail including the new banner URL.
            """)]
    [SwaggerResponse(StatusCodes.Status200OK, "Banner uploaded", typeof(EventDetailResponse))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Invalid or missing file", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Not authenticated", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Email not verified or insufficient permissions", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Event not found or deleted", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status409Conflict, "Cancelled event cannot be modified", typeof(ProblemDetails))]
    public async Task<IActionResult> UploadBanner(
        Guid eventId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            return Result.Failure<EventDetailResponse>(MediaErrors.EmptyFile).ToActionResult();
        }

        await using var stream = file.OpenReadStream();
        var upload = new MediaUploadRequest(stream, file.ContentType, file.Length, file.FileName);
        var result = await uploadEventBannerHandler.Handle(
            new UploadEventBannerCommand(eventId, upload),
            cancellationToken);

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
            Requires at least one location segment; segment startsAt/endsAt are optional but validated when set.
            Physical venue double-booking: rejects publish when another published event occupies the same place
            at overlapping effective times (Events.VenueConflict).
            Paid admission is allowed without ticket types until the tickets phase.
            Returns 200 with full published event detail.
            """)]
    [SwaggerResponse(StatusCodes.Status200OK, "Event published", typeof(EventDetailResponse))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Not authenticated", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Email not verified or insufficient permissions", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Event not found or deleted", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Publish validation failed (incomplete draft)", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status409Conflict, "Already published, rate limit exceeded, cancelled, or Events.VenueConflict", typeof(ProblemDetails))]
    public async Task<IActionResult> Publish(Guid eventId, CancellationToken cancellationToken)
    {
        var result = await publishEventHandler.Handle(new PublishEventCommand(eventId), cancellationToken);
        return result.ToActionResult();
    }

    [Authorize]
    [HttpPost("{eventId:guid}/cancel")]
    [SwaggerOperation(
        Summary = "Cancel a published event",
        Description = """
            Sets event status to Cancelled. Requires verified email and edit permission (host, creator, or group Organizer+).
            Only published events can be cancelled; use DELETE for draft events. Refunds and attendee notifications are async (Phase 7).
            Returns 204 on success.
            """)]
    [SwaggerResponse(StatusCodes.Status204NoContent, "Event cancelled")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Not authenticated", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Email not verified or insufficient permissions", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Event not found or deleted", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status409Conflict, "Event is not published (draft or already cancelled)", typeof(ProblemDetails))]
    public async Task<IActionResult> Cancel(Guid eventId, CancellationToken cancellationToken)
    {
        var result = await cancelEventHandler.Handle(new CancelEventCommand(eventId), cancellationToken);
        return result.ToActionResult();
    }

    [Authorize]
    [HttpDelete("{eventId:guid}")]
    [SwaggerOperation(
        Summary = "Soft-delete a draft event",
        Description = """
            Soft-deletes a draft event (sets DeletedAt). Requires verified email and edit permission.
            Published or cancelled events cannot be deleted; use POST .../cancel for live events. Returns 204 on success;
            404 if already deleted.
            """)]
    [SwaggerResponse(StatusCodes.Status204NoContent, "Draft event deleted")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Not authenticated", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Email not verified or insufficient permissions", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Event not found or already deleted", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status409Conflict, "Event is not a draft", typeof(ProblemDetails))]
    public async Task<IActionResult> Delete(Guid eventId, CancellationToken cancellationToken)
    {
        var result = await deleteEventHandler.Handle(new DeleteEventCommand(eventId), cancellationToken);
        return result.ToActionResult();
    }
}

/// <summary>Partial event update body. locations[] replaces the full list when sent; see PATCH action for segment-time rules.</summary>
public sealed record UpdateEventRequest(
    string? Title = null,
    string? Description = null,
    Guid? CategoryId = null,
    string? BannerImageUrl = null,
    EventTier? Tier = null,
    /// <summary>Replace-all location segments (wizard screen 4). Optional per-segment startsAt/endsAt.</summary>
    IReadOnlyList<EventLocationResponse>? Locations = null,
    DateTime? StartTime = null,
    DateTime? EndTime = null,
    string? TimeZoneId = null,
    AdmissionType? AdmissionType = null);
