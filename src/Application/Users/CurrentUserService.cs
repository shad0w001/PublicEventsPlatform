using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Users;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace Application.Users;

public sealed class CurrentUserService(
    IApplicationDbContext context,
    IUserIdentityAccessor identityAccessor,
    IOptions<UserProfileOptions> userProfileOptions) : ICurrentUserService
{
    public async Task<Result<User>> GetOrProvisionAsync(CancellationToken cancellationToken = default)
    {
        if (!identityAccessor.IsAuthenticated)
        {
            return Result.Failure<User>(UserErrors.Unauthorized());
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

        return user;
    }
}
