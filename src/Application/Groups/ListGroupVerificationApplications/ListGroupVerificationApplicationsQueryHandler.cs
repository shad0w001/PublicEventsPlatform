using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Groups.ListGroupVerificationApplications;

internal sealed class ListGroupVerificationApplicationsQueryHandler(
    IApplicationDbContext context,
    IUserIdentityAccessor identityAccessor)
    : IQueryHandler<ListGroupVerificationApplicationsQuery, IReadOnlyList<GroupVerificationApplicationResponse>>
{
    public async Task<Result<IReadOnlyList<GroupVerificationApplicationResponse>>> Handle(
        ListGroupVerificationApplicationsQuery query,
        CancellationToken cancellationToken)
    {
        var gateResult = AdminGate.EnsureAdmin(identityAccessor);
        if (gateResult.IsFailure)
        {
            return Result.Failure<IReadOnlyList<GroupVerificationApplicationResponse>>(gateResult.Error);
        }

        var applications = await context.GroupVerificationApplications
            .AsNoTracking()
            .OrderByDescending(a => a.SubmittedAt)
            .Join(
                context.Groups,
                application => application.GroupId,
                group => group.Id,
                (application, group) => new GroupVerificationApplicationResponse(
                    application.Id,
                    application.GroupId,
                    group.Name,
                    application.SubmittedByUserId,
                    application.Status,
                    application.SubmittedAt,
                    application.DecidedAt,
                    application.DecidedByUserId))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<GroupVerificationApplicationResponse>>(applications);
    }
}
