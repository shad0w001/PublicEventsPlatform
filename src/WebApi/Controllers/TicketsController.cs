using Application.Abstractions.Messaging;
using Application.Tickets.GetTicket;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using WebApi.Extensions;

namespace WebApi.Controllers;

[ApiController]
[Route("api/tickets")]
[SwaggerTag("Tickets")]
public sealed class TicketsController(
    IQueryHandler<GetTicketQuery, TicketDetailResponse> getTicketHandler) : ControllerBase
{
    [Authorize]
    [HttpGet("{ticketId:guid}")]
    [SwaggerOperation(
        Summary = "Get ticket detail for QR / manual code display",
        Description = """
            Returns minimal ticket detail for the ticket QR page.
            Requires verified email. Caller must own the ticket (personal purchase) or be Organizer+ on a group buyer.
            state Active exposes manualCode and qrPayload (opaque ticket UUID); Used and RefundPending omit codes — the SPA derives copy from state.
            """)]
    [SwaggerResponse(StatusCodes.Status200OK, "Ticket detail", typeof(TicketDetailResponse))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Not authenticated", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Not authorized to view this ticket", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Ticket not found", typeof(ProblemDetails))]
    public async Task<IActionResult> Get(Guid ticketId, CancellationToken cancellationToken)
    {
        var result = await getTicketHandler.Handle(new GetTicketQuery(ticketId), cancellationToken);
        return result.ToActionResult();
    }
}
