using Domain.Users;

namespace Infrastructure.Authentication;

public static class ServiceRoleMapper
{
    public static ServiceRole FromAuth0Roles(IEnumerable<string> roles) =>
        roles.Any(role => string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase))
            ? ServiceRole.Admin
            : ServiceRole.User;
}
