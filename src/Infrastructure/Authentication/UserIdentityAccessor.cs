using Application.Abstractions.Authentication;
using Domain.Users;
using Microsoft.AspNetCore.Http;

namespace Infrastructure.Authentication;

public sealed class UserIdentityAccessor(IHttpContextAccessor httpContextAccessor) : IUserIdentityAccessor
{
    public bool IsAuthenticated =>
        httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;

    public string ExternalSubjectId =>
        httpContextAccessor.HttpContext?.User.GetSubjectId() ?? string.Empty;

    public string Email =>
        httpContextAccessor.HttpContext?.User.GetEmail() ?? string.Empty;

    public bool EmailVerified =>
        httpContextAccessor.HttpContext?.User.GetEmailVerified() ?? false;

    public ServiceRole ServiceRole =>
        ServiceRoleMapper.FromAuth0Roles(
            httpContextAccessor.HttpContext?.User.GetRoles() ?? []);

    public string? ProfilePictureUrl =>
        httpContextAccessor.HttpContext?.User.GetPictureUrl();
}
