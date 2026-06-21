using Application.Abstractions.Authentication;
using Domain.Users;
using SharedKernel;

namespace Application.Abstractions.Authentication;

public static class AdminGate
{
    public static Result EnsureAdmin(IUserIdentityAccessor identity)
    {
        var authResult = VerifiedUserGate.EnsureAuthenticated(identity);
        if (authResult.IsFailure)
        {
            return authResult;
        }

        if (identity.ServiceRole != ServiceRole.Admin)
        {
            return Result.Failure(UserErrors.InsufficientAdminPermissions());
        }

        return Result.Success();
    }
}
