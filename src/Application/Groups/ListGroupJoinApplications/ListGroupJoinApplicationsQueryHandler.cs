using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Groups.Services;
using Application.Users.Services;
using Domain.Groups;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Groups.ListGroupJoinApplications;

internal sealed class ListGroupJoinApplicationsQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IUserIdentityAccessor identityAccessor,
    GroupAccessService groupAccessService)
    : IQueryHandler<ListGroupJoinApplicationsQuery, IReadOnlyList<GroupJoinApplicationResponse>>
{
    public async Task<Result<IReadOnlyList<GroupJoinApplicationResponse>>> Handle(
        ListGroupJoinApplicationsQuery query,
        CancellationToken cancellationToken)
    {
        var gateResult = VerifiedUserGate.EnsureVerified(identityAccessor);
        if (gateResult.IsFailure)
        {
            return Result.Failure<IReadOnlyList<GroupJoinApplicationResponse>>(gateResult.Error);
        }

        var userResult = await currentUserService.GetOrProvisionAsync(cancellationToken);
        if (userResult.IsFailure)
        {
            return Result.Failure<IReadOnlyList<GroupJoinApplicationResponse>>(userResult.Error);
        }

        var groupResult = await groupAccessService.GetActiveGroupAsync(query.GroupId, cancellationToken);
        if (groupResult.IsFailure)
        {
            return Result.Failure<IReadOnlyList<GroupJoinApplicationResponse>>(groupResult.Error);
        }

        var group = groupResult.Value;
        var userId = userResult.Value.Id;
        var membership = group.GroupMemberships.FirstOrDefault(m => m.UserId == userId);
        var canReview = membership is not null &&
                        GroupPermissions.CanReviewApplications(membership.Role);

        IQueryable<GroupJoinApplication> applicationsQuery =
            context.GroupJoinApplications.AsNoTracking().Where(a => a.GroupId == query.GroupId);

        if (!canReview)
        {
            applicationsQuery = applicationsQuery.Where(a => a.UserId == userId);
        }

        var applications = await applicationsQuery
            .OrderByDescending(a => a.SubmittedAt)
            .Join(
                context.Users,
                application => application.UserId,
                user => user.Id,
                (application, user) => new GroupJoinApplicationResponse(
                    application.Id,
                    application.UserId,
                    user.Username,
                    application.Status,
                    application.SubmittedAt,
                    application.DecidedAt,
                    application.DecidedByUserId))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<GroupJoinApplicationResponse>>(applications);
    }
}
