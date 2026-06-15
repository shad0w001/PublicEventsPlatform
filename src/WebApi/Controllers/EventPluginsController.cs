using Application.Abstractions.Messaging;
using Application.Plugins;
using Application.Plugins.AttachEventPlugin;
using Application.Plugins.DetachEventPlugin;
using Application.Plugins.UpdateEventPlugin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using WebApi.Extensions;

namespace WebApi.Controllers;

[ApiController]
[Route("api/events/{eventId:guid}/plugins")]
[SwaggerTag("EventPlugins")]
public sealed class EventPluginsController(
    ICommandHandler<AttachEventPluginCommand, EventPluginResponse> attachHandler,
    ICommandHandler<UpdateEventPluginCommand, EventPluginResponse> updateHandler,
    ICommandHandler<DetachEventPluginCommand> detachHandler) : ControllerBase
{
    [Authorize]
    [HttpPost("{pluginId:guid}")]
    [SwaggerOperation(
        Summary = "Attach a catalog plugin to an event",
        Description = """
            Attaches a plugin from the catalog to a big-tier event with initial configuration data.
            Requires verified email and event edit permission (same as PATCH /api/events/{eventId}).
            Big tier only; at most 5 plugins per event; at least one data key required; validated per plugin code.
            Returns 201 with EventPluginResponse. Errors: Plugins.NotFound, Plugins.BigTierRequired,
            Plugins.MaxPluginsExceeded, Plugins.AlreadyAttached, Plugins.DataRequired, Plugins.InvalidData,
            Events.CannotModifyCancelled (409), Events.InsufficientPermissions (403).
            """)]
    [SwaggerResponse(StatusCodes.Status201Created, "Plugin attached", typeof(EventPluginResponse))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Not authenticated", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Email not verified or insufficient permissions", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Event or plugin not found", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Validation failed", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status409Conflict, "Already attached or event cancelled", typeof(ProblemDetails))]
    public async Task<IActionResult> Attach(
        Guid eventId,
        Guid pluginId,
        [FromBody] EventPluginDataRequest body,
        CancellationToken cancellationToken)
    {
        var command = new AttachEventPluginCommand(eventId, pluginId, body.Data);
        var result = await attachHandler.Handle(command, cancellationToken);

        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Created($"/api/events/{eventId}/plugins/{pluginId}", result.Value);
    }

    [Authorize]
    [HttpPatch("{pluginId:guid}")]
    [SwaggerOperation(
        Summary = "Replace plugin configuration on an event",
        Description = """
            Replaces all configuration data for an attached plugin (replace-all PATCH).
            Requires verified email and event edit permission.
            Data is validated per plugin code using the event's current start/end times.
            Returns 200 with EventPluginResponse. Errors: Plugins.NotAttached, Plugins.DataRequired,
            Plugins.InvalidData, Events.CannotModifyCancelled (409), Events.InsufficientPermissions (403).
            """)]
    [SwaggerResponse(StatusCodes.Status200OK, "Plugin configuration updated", typeof(EventPluginResponse))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Not authenticated", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Email not verified or insufficient permissions", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Event, plugin, or attachment not found", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Validation failed", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status409Conflict, "Event cancelled", typeof(ProblemDetails))]
    public async Task<IActionResult> Update(
        Guid eventId,
        Guid pluginId,
        [FromBody] EventPluginDataRequest body,
        CancellationToken cancellationToken)
    {
        var command = new UpdateEventPluginCommand(eventId, pluginId, body.Data);
        var result = await updateHandler.Handle(command, cancellationToken);
        return result.ToActionResult();
    }

    [Authorize]
    [HttpDelete("{pluginId:guid}")]
    [SwaggerOperation(
        Summary = "Detach a plugin from an event",
        Description = """
            Removes a plugin attachment and its configuration from the event.
            Requires verified email and event edit permission.
            Returns 204 on success. Errors: Plugins.NotAttached, Events.CannotModifyCancelled (409),
            Events.InsufficientPermissions (403).
            """)]
    [SwaggerResponse(StatusCodes.Status204NoContent, "Plugin detached")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Not authenticated", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Email not verified or insufficient permissions", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Event or attachment not found", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status409Conflict, "Event cancelled", typeof(ProblemDetails))]
    public async Task<IActionResult> Detach(
        Guid eventId,
        Guid pluginId,
        CancellationToken cancellationToken)
    {
        var command = new DetachEventPluginCommand(eventId, pluginId);
        var result = await detachHandler.Handle(command, cancellationToken);
        return result.ToActionResult();
    }
}
