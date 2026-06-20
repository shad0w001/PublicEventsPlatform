using Application.Abstractions.Messaging;
using Application.Tickets.GetOrder;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using WebApi.Extensions;

namespace WebApi.Controllers;

[ApiController]
[Route("api/orders")]
[SwaggerTag("Orders")]
public sealed class OrdersController(
    IQueryHandler<GetOrderQuery, OrderConfirmationResponse> getOrderHandler) : ControllerBase
{
    [Authorize]
    [HttpGet("{orderId:guid}")]
    [SwaggerOperation(
        Summary = "Get order confirmation details",
        Description = """
            Returns purchase confirmation for a paid order (Stripe success redirect target).
            Requires verified email. Caller must be the buyer (self) or Organizer+ on a group buyer.
            Rich event summary (title, banner, times, host) plus ticket ids for navigation to ticket pages.
            refundPending is true when the event was cancelled but the order is still Paid (refund handled in Phase 7).
            Pending or expired orders return 409 Tickets.OrderNotPaid.
            """)]
    [SwaggerResponse(StatusCodes.Status200OK, "Order confirmation", typeof(OrderConfirmationResponse))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Not authenticated", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Not the buyer", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Order not found", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status409Conflict, "Order not paid", typeof(ProblemDetails))]
    public async Task<IActionResult> Get(Guid orderId, CancellationToken cancellationToken)
    {
        var result = await getOrderHandler.Handle(new GetOrderQuery(orderId), cancellationToken);
        return result.ToActionResult();
    }
}
