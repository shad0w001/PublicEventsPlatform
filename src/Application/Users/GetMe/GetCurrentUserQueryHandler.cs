using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace Application.Users.GetMe;

public sealed class GetCurrentUserQueryHandler(
    IApplicationDbContext context,
    IUserIdentityAccessor identityAccessor,
    IOptions<UserProfileOptions> userProfileOptions) : IQueryHandler<GetCurrentUserQuery, UserResponse>
{
    public async Task<Result<UserResponse>> Handle(
        GetCurrentUserQuery query,
        CancellationToken cancellationToken)
    {
        if (!identityAccessor.IsAuthenticated)
        {
            return Result.Failure<UserResponse>(UserErrors.Unauthorized());
        }

        var user = await context.Users
            .SingleOrDefaultAsync(
                u => u.ExternalSubjectId == identityAccessor.ExternalSubjectId,
                cancellationToken);

        if (user is null)
        {
            user = User.CreateFromExternalIdentity(
                identityAccessor.ExternalSubjectId,
                identityAccessor.Email,
                identityAccessor.EmailVerified,
                identityAccessor.ProfilePictureUrl,
                userProfileOptions.Value.DefaultAvatarUrl,
                identityAccessor.ServiceRole);

            context.Users.Add(user);
        }
        else
        {
            user.SyncFromExternalIdentity(
                identityAccessor.Email,
                identityAccessor.EmailVerified,
                identityAccessor.ProfilePictureUrl,
                identityAccessor.ServiceRole);
        }

        await context.SaveChangesAsync(cancellationToken);

        return MapToResponse(user);
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
