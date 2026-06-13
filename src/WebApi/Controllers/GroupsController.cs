using Application.Abstractions.Messaging;
using Application.Groups.CreateGroup;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApi.Extensions;

namespace WebApi.Controllers;

[ApiController]
[Route("api/groups")]
public sealed class GroupsController(
    ICommandHandler<CreateGroupCommand, GroupResponse> createGroupHandler) : ControllerBase
{
    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateGroupCommand command,
        CancellationToken cancellationToken)
    {
        var result = await createGroupHandler.Handle(command, cancellationToken);
        return result.ToActionResult();
    }
}
