using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Users.Services;
using Domain.Subscriptions.Services;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Subscriptions.CreateSubscription;

internal sealed class CreateSubscriptionCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IUserIdentityAccessor identityAccessor)
    : ICommandHandler<CreateSubscriptionCommand, UserSubscriptionResponse>
{
    public async Task<Result<UserSubscriptionResponse>> Handle(
        CreateSubscriptionCommand command,
        CancellationToken cancellationToken)
    {
        var gateResult = VerifiedUserGate.EnsureVerified(identityAccessor);
        if (gateResult.IsFailure)
        {
            return Result.Failure<UserSubscriptionResponse>(gateResult.Error);
        }

        var userResult = await currentUserService.GetOrProvisionAsync(cancellationToken);
        if (userResult.IsFailure)
        {
            return Result.Failure<UserSubscriptionResponse>(userResult.Error);
        }

        var user = userResult.Value;

        var existingSubscriptions = await context.UserSubscriptions
            .Where(s => s.UserId == user.Id)
            .ToListAsync(cancellationToken);

        var categoryExists = command.CategoryId is not null &&
                             await context.EventCategories
                                 .AnyAsync(c => c.Id == command.CategoryId, cancellationToken);

        var createResult = SubscriptionService.Create(
            user.Id,
            command.Kind,
            command.City,
            command.CategoryId,
            categoryExists,
            existingSubscriptions);

        if (createResult.IsFailure)
        {
            return Result.Failure<UserSubscriptionResponse>(createResult.Error);
        }

        var subscription = createResult.Value;
        context.UserSubscriptions.Add(subscription);
        await context.SaveChangesAsync(cancellationToken);

        string? categoryName = null;
        if (subscription.CategoryId is not null)
        {
            categoryName = await context.EventCategories
                .AsNoTracking()
                .Where(c => c.Id == subscription.CategoryId)
                .Select(c => c.Name)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return SubscriptionMapping.Map(subscription, categoryName);
    }
}
