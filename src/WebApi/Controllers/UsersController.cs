using Application.Abstractions.Messaging;
using Application.Events;
using Application.Events.ListMyRsvps;
using Application.Users;
using Application.Users.GetMe;
using Application.Users.ListMyJoinApplications;
using Application.Tickets.ListMyTickets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using WebApi.Extensions;

namespace WebApi.Controllers;

[ApiController]
[Route("api/users")]
[SwaggerTag("Users")]
public sealed class UsersController(
    IQueryHandler<GetCurrentUserQuery, UserResponse> getMeHandler,
    IQueryHandler<ListMyJoinApplicationsQuery, IReadOnlyList<MyJoinApplicationResponse>> listMyApplicationsHandler,
    IQueryHandler<ListMyRsvpsQuery, IReadOnlyList<MyRsvpListItemResponse>> listMyRsvpsHandler,
    IQueryHandler<ListMyTicketsQuery, MyTicketsResponse> listMyTicketsHandler)
    : ControllerBase
{
    [Authorize]
    [HttpGet("me")]
    [SwaggerOperation(
        Summary = "Get current user profile",
        Description = """
            Returns the local user row for the JWT subject, provisioning or syncing from Auth0 claims on first call.
            Does not require verified email. Includes email, verification flag, username, bio, avatar, and service role.
            """)]
    [SwaggerResponse(StatusCodes.Status200OK, "Current user", typeof(UserResponse))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Not authenticated", typeof(ProblemDetails))]
    public async Task<IActionResult> GetMe(CancellationToken cancellationToken)
    {
        var result = await getMeHandler.Handle(new GetCurrentUserQuery(), cancellationToken);
        return result.ToActionResult();
    }

    [Authorize]
    [HttpGet("me/applications")]
    [SwaggerOperation(
        Summary = "List my group join applications",
        Description = """
            Returns all join applications submitted by the caller across every group, newest first.
            Requires verified email. Each item includes group id and name for navigation.
            """)]
    [SwaggerResponse(StatusCodes.Status200OK, "Applications", typeof(IReadOnlyList<MyJoinApplicationResponse>))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Not authenticated", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Email not verified", typeof(ProblemDetails))]
    public async Task<IActionResult> ListMyApplications(CancellationToken cancellationToken)
    {
        var result = await listMyApplicationsHandler.Handle(new ListMyJoinApplicationsQuery(), cancellationToken);
        return result.ToActionResult();
    }

    [Authorize]
    [HttpGet("me/rsvps")]
    [SwaggerOperation(
        Summary = "List my free-event RSVPs",
        Description = """
            Returns free events where the caller has an EventAttendee row as themselves or as an Organizer+ group.
            Requires verified email. Includes Going and Interested only (NotGoing excluded).
            Events must be published or cancelled; drafts and soft-deleted events are omitted.
            Sorted by startTime ascending, then registeredAt descending.
            Paid tickets are listed separately in Phase 6 (/me/tickets); the SPA merges both on "My tickets / RSVPs".
            """)]
    [SwaggerResponse(StatusCodes.Status200OK, "RSVP list", typeof(IReadOnlyList<MyRsvpListItemResponse>))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Not authenticated", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Email not verified", typeof(ProblemDetails))]
    public async Task<IActionResult> ListMyRsvps(CancellationToken cancellationToken)
    {
        var result = await listMyRsvpsHandler.Handle(new ListMyRsvpsQuery(), cancellationToken);
        return result.ToActionResult();
    }

    [Authorize]
    [HttpGet("me/tickets")]
    [SwaggerOperation(
        Summary = "List my paid tickets",
        Description = """
            Returns individual paid tickets for the caller as personal purchases and as Organizer+ group purchases.
            Requires verified email. Inventory list only (no QR/manual codes — use GET /api/tickets/{ticketId}).
            Includes published and cancelled events (refundPending when event cancelled); excludes draft and soft-deleted events.
            Hides non-Paid orders (refunded tickets excluded once Phase 7 marks orders).
            Sorted by event startTime ascending, then ticket created descending.
            """)]
    [SwaggerResponse(StatusCodes.Status200OK, "Ticket list", typeof(MyTicketsResponse))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Not authenticated", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Email not verified", typeof(ProblemDetails))]
    public async Task<IActionResult> ListMyTickets(CancellationToken cancellationToken)
    {
        var result = await listMyTicketsHandler.Handle(new ListMyTicketsQuery(), cancellationToken);
        return result.ToActionResult();
    }
}
