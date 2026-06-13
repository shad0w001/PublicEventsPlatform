using Application.Abstractions.Authentication;
using Domain.Users;
using SharedKernel;

namespace Application.Abstractions.Authentication;

public static class VerifiedUserGate
{
    public static Result EnsureAuthenticated(IUserIdentityAccessor identity)
    {
        if (!identity.IsAuthenticated)
        {
            return Result.Failure(UserErrors.Unauthorized());
        }

        return Result.Success();
    }

    public static Result EnsureVerified(IUserIdentityAccessor identity)
    {
        var authResult = EnsureAuthenticated(identity);
        if (authResult.IsFailure)
        {
            return authResult;
        }

        if (!identity.EmailVerified)
        {
            return Result.Failure(UserErrors.EmailNotVerified());
        }

        return Result.Success();
    }
}
