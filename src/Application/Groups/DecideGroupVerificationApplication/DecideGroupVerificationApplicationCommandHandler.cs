using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Groups.Services;
using Application.Users.Services;
using Domain.Groups;
using Domain.Groups.Services;
using SharedKernel;

namespace Application.Groups.DecideGroupVerificationApplication;

internal sealed class DecideGroupVerificationApplicationCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IUserIdentityAccessor identityAccessor,
    GroupAccessService groupAccessService)
    : ICommandHandler<DecideGroupVerificationApplicationCommand>
{
    public async Task<Result> Handle(
        DecideGroupVerificationApplicationCommand command,
        CancellationToken cancellationToken)
    {
        var gateResult = AdminGate.EnsureAdmin(identityAccessor);
        if (gateResult.IsFailure)
        {
            return gateResult;
        }

        var userResult = await currentUserService.GetOrProvisionAsync(cancellationToken);
        if (userResult.IsFailure)
        {
            return Result.Failure(userResult.Error);
        }

        var loadResult = await groupAccessService.GetGroupWithVerificationApplicationAsync(
            command.ApplicationId,
            cancellationToken);
        if (loadResult.IsFailure)
        {
            return Result.Failure(loadResult.Error);
        }

        var (group, application) = loadResult.Value;
        var utcNow = DateTime.UtcNow;
        var decidedByUserId = userResult.Value.Id;

        Result decideResult = command.Decision switch
        {
            VerificationApplicationDecision.Approve => GroupVerificationService.ApproveApplication(
                group,
                application.Id,
                decidedByUserId,
                utcNow),
            VerificationApplicationDecision.Reject => GroupVerificationService.RejectApplication(
                group,
                application.Id,
                decidedByUserId,
                utcNow),
            _ => Result.Failure(GroupErrors.InsufficientPermissions())
        };

        if (decideResult.IsFailure)
        {
            return decideResult;
        }

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
