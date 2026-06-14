using Application.Abstractions.Messaging;
using Application.Users;
using Application.Users.GetMe;
using Application.Users.ListMyJoinApplications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApi.Extensions;

namespace WebApi.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController(
    IQueryHandler<GetCurrentUserQuery, UserResponse> getMeHandler,
    IQueryHandler<ListMyJoinApplicationsQuery, IReadOnlyList<MyJoinApplicationResponse>> listMyApplicationsHandler)
    : ControllerBase
{
    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetMe(CancellationToken cancellationToken)
    {
        var result = await getMeHandler.Handle(new GetCurrentUserQuery(), cancellationToken);
        return result.ToActionResult();
    }

    [Authorize]
    [HttpGet("me/applications")]
    public async Task<IActionResult> ListMyApplications(CancellationToken cancellationToken)
    {
        var result = await listMyApplicationsHandler.Handle(new ListMyJoinApplicationsQuery(), cancellationToken);
        return result.ToActionResult();
    }
}
