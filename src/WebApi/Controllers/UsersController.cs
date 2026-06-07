using Application.Abstractions.Messaging;
using Application.Users.GetMe;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApi.Extensions;

namespace WebApi.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController(IQueryHandler<GetCurrentUserQuery, UserResponse> handler) : ControllerBase
{
    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetMe(CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new GetCurrentUserQuery(), cancellationToken);
        return result.ToActionResult();
    }
}
