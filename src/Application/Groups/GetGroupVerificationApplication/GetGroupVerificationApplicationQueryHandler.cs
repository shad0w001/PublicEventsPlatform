using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Groups.Services;
using Domain.Groups;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Groups.GetGroupVerificationApplication;

internal sealed class GetGroupVerificationApplicationQueryHandler(
    IApplicationDbContext context,
    IUserIdentityAccessor identityAccessor,
    GroupVerificationEligibilityService eligibilityService)
    : IQueryHandler<GetGroupVerificationApplicationQuery, GroupVerificationApplicationDetailResponse>
{
    public async Task<Result<GroupVerificationApplicationDetailResponse>> Handle(
        GetGroupVerificationApplicationQuery query,
        CancellationToken cancellationToken)
    {
        var gateResult = AdminGate.EnsureAdmin(identityAccessor);
        if (gateResult.IsFailure)
        {
            return Result.Failure<GroupVerificationApplicationDetailResponse>(gateResult.Error);
        }

        var application = await context.GroupVerificationApplications
            .AsNoTracking()
            .Where(a => a.Id == query.ApplicationId)
            .Join(
                context.Groups,
                a => a.GroupId,
                g => g.Id,
                (a, g) => new
                {
                    Application = a,
                    Group = g,
                    MemberCount = g.GroupMemberships.Count
                })
            .FirstOrDefaultAsync(cancellationToken);

        if (application is null)
        {
            return Result.Failure<GroupVerificationApplicationDetailResponse>(
                GroupVerificationApplicationErrors.NotFound(query.ApplicationId));
        }

        if (application.Group.DeletedAt is not null)
        {
            return Result.Failure<GroupVerificationApplicationDetailResponse>(
                GroupErrors.Deleted(application.Group.Id));
        }

        var snapshot = await eligibilityService.GetSnapshotAsync(
            application.Group.Id,
            DateTime.UtcNow,
            cancellationToken);

        return new GroupVerificationApplicationDetailResponse(
            application.Application.Id,
            application.Group.Id,
            application.Group.Name,
            application.Group.Description,
            application.Group.ProfileImageUrl,
            application.MemberCount,
            application.Group.IsVerified,
            application.Application.SubmittedByUserId,
            application.Application.Status,
            application.Application.SubmittedAt,
            application.Application.DecidedAt,
            application.Application.DecidedByUserId,
            snapshot);
    }
}
