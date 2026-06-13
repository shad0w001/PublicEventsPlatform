using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Domain.Users;
using SharedKernel;

namespace Application.Users.GetMe;

public sealed class GetCurrentUserQueryHandler(
    ICurrentUserService currentUserService,
    IUserIdentityAccessor identityAccessor) : IQueryHandler<GetCurrentUserQuery, UserResponse>
{
    public async Task<Result<UserResponse>> Handle(
        GetCurrentUserQuery query,
        CancellationToken cancellationToken)
    {
        if (!identityAccessor.IsAuthenticated)
        {
            return Result.Failure<UserResponse>(UserErrors.Unauthorized());
        }

        var userResult = await currentUserService.GetOrProvisionAsync(cancellationToken);
        if (userResult.IsFailure)
        {
            return Result.Failure<UserResponse>(userResult.Error);
        }

        return MapToResponse(userResult.Value);
    }

    private static UserResponse MapToResponse(User user) =>
        new(
            user.Id,
            user.Email,
            user.EmailVerified,
            user.Username,
            user.Bio,
            user.ProfilePictureUrl,
            user.ServiceRole,
            user.CreatedAt,
            user.LastActive);
}
