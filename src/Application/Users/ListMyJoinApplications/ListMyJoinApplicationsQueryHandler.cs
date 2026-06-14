using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Users.Services;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.ListMyJoinApplications;

internal sealed class ListMyJoinApplicationsQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IUserIdentityAccessor identityAccessor)
    : IQueryHandler<ListMyJoinApplicationsQuery, IReadOnlyList<MyJoinApplicationResponse>>
{
    public async Task<Result<IReadOnlyList<MyJoinApplicationResponse>>> Handle(
        ListMyJoinApplicationsQuery query,
        CancellationToken cancellationToken)
    {
        var gateResult = VerifiedUserGate.EnsureVerified(identityAccessor);
        if (gateResult.IsFailure)
        {
            return Result.Failure<IReadOnlyList<MyJoinApplicationResponse>>(gateResult.Error);
        }

        var userResult = await currentUserService.GetOrProvisionAsync(cancellationToken);
        if (userResult.IsFailure)
        {
            return Result.Failure<IReadOnlyList<MyJoinApplicationResponse>>(userResult.Error);
        }

        var applications = await context.GroupJoinApplications
            .AsNoTracking()
            .Where(a => a.UserId == userResult.Value.Id)
            .OrderByDescending(a => a.SubmittedAt)
            .Join(
                context.Groups,
                application => application.GroupId,
                group => group.Id,
                (application, group) => new MyJoinApplicationResponse(
                    application.Id,
                    application.GroupId,
                    group.Name,
                    application.Status,
                    application.SubmittedAt,
                    application.DecidedAt,
                    application.DecidedByUserId))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<MyJoinApplicationResponse>>(applications);
    }
}
