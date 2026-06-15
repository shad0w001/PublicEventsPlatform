using Application.Abstractions.Messaging;
using Application.Plugins.ListPlugins;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using WebApi.Extensions;

namespace WebApi.Controllers;

[ApiController]
[Route("api/plugins")]
[SwaggerTag("Plugins")]
public sealed class PluginsController(
    IQueryHandler<ListPluginsQuery, IReadOnlyList<PluginCatalogItemResponse>> listPluginsHandler)
    : ControllerBase
{
    [HttpGet]
    [SwaggerOperation(
        Summary = "List the plugin catalog",
        Description = """
            Returns the full dev-team plugin catalog for the event creation wizard (big tier, screen 7)
            and public page renderer selection. No authentication required. Sorted by name.
            Each item includes stable id (for attach routes) and code (for SPA renderer mapping).
            """)]
    [SwaggerResponse(StatusCodes.Status200OK, "Plugin catalog", typeof(IReadOnlyList<PluginCatalogItemResponse>))]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var result = await listPluginsHandler.Handle(new ListPluginsQuery(), cancellationToken);
        return result.ToActionResult();
    }
}
