using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.Events;
using Application.Events.BrowseEvents;
using Application.Events.GetMyFeed;
using Application.Events.ListMyRsvps;
using Application.Subscriptions;
using Application.Subscriptions.CreateSubscription;
using Application.Subscriptions.DeleteSubscription;
using Application.Subscriptions.ListMySubscriptions;
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
    IQueryHandler<ListMyTicketsQuery, MyTicketsResponse> listMyTicketsHandler,
    ICommandHandler<CreateSubscriptionCommand, UserSubscriptionResponse> createSubscriptionHandler,
    IQueryHandler<ListMySubscriptionsQuery, IReadOnlyList<UserSubscriptionResponse>> listMySubscriptionsHandler,
    ICommandHandler<DeleteSubscriptionCommand> deleteSubscriptionHandler,
    IQueryHandler<GetMyFeedQuery, PagedResult<EventBrowseCardResponse>> getMyFeedHandler)
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
            Paid tickets are listed separately at GET /api/users/me/tickets; the SPA merges both on "My tickets / RSVPs".
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
            Excludes non-Paid orders (refunded order status is not modeled in v1).
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

    [Authorize]
    [HttpPost("me/subscriptions")]
    [SwaggerOperation(
        Summary = "Create a subscription",
        Description = """
            Adds a discovery subscription for the caller. Requires verified email.
            Kinds: City (city required), Category (categoryId required), Online (no extra fields).
            Max 30 subscriptions per user; duplicates per kind+value return 409.
            City values are normalized server-side for matching.
            """)]
    [SwaggerResponse(StatusCodes.Status201Created, "Subscription created", typeof(UserSubscriptionResponse))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Not authenticated", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Email not verified", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Invalid payload or unknown category", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status409Conflict, "Duplicate or max limit reached", typeof(ProblemDetails))]
    public async Task<IActionResult> CreateSubscription(
        [FromBody] CreateSubscriptionCommand command,
        CancellationToken cancellationToken)
    {
        var result = await createSubscriptionHandler.Handle(command, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Created($"/api/users/me/subscriptions/{result.Value.Id}", result.Value);
    }

    [Authorize]
    [HttpGet("me/subscriptions")]
    [SwaggerOperation(
        Summary = "List my subscriptions",
        Description = """
            Returns all discovery subscriptions for the caller. Requires verified email.
            Sorted by createdAt descending (newest first). Category subscriptions include categoryName.
            """)]
    [SwaggerResponse(StatusCodes.Status200OK, "Subscription list", typeof(IReadOnlyList<UserSubscriptionResponse>))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Not authenticated", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Email not verified", typeof(ProblemDetails))]
    public async Task<IActionResult> ListMySubscriptions(CancellationToken cancellationToken)
    {
        var result = await listMySubscriptionsHandler.Handle(new ListMySubscriptionsQuery(), cancellationToken);
        return result.ToActionResult();
    }

    [Authorize]
    [HttpDelete("me/subscriptions/{subscriptionId:guid}")]
    [SwaggerOperation(
        Summary = "Delete a subscription",
        Description = """
            Removes one subscription owned by the caller. Requires verified email.
            Returns 404 when the subscription id is missing or belongs to another user.
            """)]
    [SwaggerResponse(StatusCodes.Status204NoContent, "Subscription deleted")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Not authenticated", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Email not verified", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Subscription not found", typeof(ProblemDetails))]
    public async Task<IActionResult> DeleteSubscription(
        Guid subscriptionId,
        CancellationToken cancellationToken)
    {
        var result = await deleteSubscriptionHandler.Handle(
            new DeleteSubscriptionCommand(subscriptionId),
            cancellationToken);
        return result.ToActionResult();
    }

    [Authorize]
    [HttpGet("me/feed")]
    [SwaggerOperation(
        Summary = "Get my subscription feed",
        Description = """
            Returns published future events matching the caller's discovery subscriptions. Requires verified email.
            Location subscriptions (city and/or online) combine with category subscriptions using AND when both exist.
            Within locations: city match OR virtual segment when Online is subscribed. Category-only subscriptions match globally.
            Zero subscriptions returns an empty paginated list. Same event card shape as GET /api/events; sorted by startTime ascending.
            SPA infinite scroll: request page=1, then page+1 on scroll and append items.
            """)]
    [SwaggerResponse(StatusCodes.Status200OK, "Feed", typeof(PagedResult<EventBrowseCardResponse>))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Not authenticated", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Email not verified", typeof(ProblemDetails))]
    public async Task<IActionResult> GetMyFeed(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = EventDiscoveryConstants.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await getMyFeedHandler.Handle(
            new GetMyFeedQuery(page, pageSize),
            cancellationToken);
        return result.ToActionResult();
    }
}
