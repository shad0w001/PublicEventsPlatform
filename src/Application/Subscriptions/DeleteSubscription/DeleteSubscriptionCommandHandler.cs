using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Users.Services;
using Domain.Subscriptions;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Subscriptions.DeleteSubscription;

internal sealed class DeleteSubscriptionCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IUserIdentityAccessor identityAccessor)
    : ICommandHandler<DeleteSubscriptionCommand>
{
    public async Task<Result> Handle(
        DeleteSubscriptionCommand command,
        CancellationToken cancellationToken)
    {
        var gateResult = VerifiedUserGate.EnsureVerified(identityAccessor);
        if (gateResult.IsFailure)
        {
            return gateResult;
        }

        var userResult = await currentUserService.GetOrProvisionAsync(cancellationToken);
        if (userResult.IsFailure)
        {
            return Result.Failure(userResult.Error);
        }

        var subscription = await context.UserSubscriptions
            .FirstOrDefaultAsync(
                s => s.Id == command.SubscriptionId && s.UserId == userResult.Value.Id,
                cancellationToken);

        if (subscription is null)
        {
            return Result.Failure(SubscriptionErrors.NotFound(command.SubscriptionId));
        }

        context.UserSubscriptions.Remove(subscription);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
