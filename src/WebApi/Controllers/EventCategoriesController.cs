using Application.Abstractions.Messaging;
using Application.EventCategories.ListEventCategories;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using WebApi.Extensions;

namespace WebApi.Controllers;

[ApiController]
[Route("api/event-categories")]
[SwaggerTag("EventCategories")]
public sealed class EventCategoriesController(
    IQueryHandler<ListEventCategoriesQuery, IReadOnlyList<EventCategoryTreeNodeResponse>> listEventCategoriesHandler)
    : ControllerBase
{
    [HttpGet]
    [SwaggerOperation(
        Summary = "List event categories as a tree",
        Description = """
            Returns the full event category catalog as a nested tree for the creation wizard picker
            and future browse filters. No authentication required. Root categories have null parent;
            each node includes subCategories (empty array when leaf). Sorted by name at each level.
            """)]
    [SwaggerResponse(StatusCodes.Status200OK, "Category tree", typeof(IReadOnlyList<EventCategoryTreeNodeResponse>))]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var result = await listEventCategoriesHandler.Handle(new ListEventCategoriesQuery(), cancellationToken);
        return result.ToActionResult();
    }
}
