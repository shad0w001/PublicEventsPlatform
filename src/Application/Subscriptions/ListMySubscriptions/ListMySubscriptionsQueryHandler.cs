using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Users.Services;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Subscriptions.ListMySubscriptions;

internal sealed class ListMySubscriptionsQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IUserIdentityAccessor identityAccessor)
    : IQueryHandler<ListMySubscriptionsQuery, IReadOnlyList<UserSubscriptionResponse>>
{
    public async Task<Result<IReadOnlyList<UserSubscriptionResponse>>> Handle(
        ListMySubscriptionsQuery query,
        CancellationToken cancellationToken)
    {
        var gateResult = VerifiedUserGate.EnsureVerified(identityAccessor);
        if (gateResult.IsFailure)
        {
            return Result.Failure<IReadOnlyList<UserSubscriptionResponse>>(gateResult.Error);
        }

        var userResult = await currentUserService.GetOrProvisionAsync(cancellationToken);
        if (userResult.IsFailure)
        {
            return Result.Failure<IReadOnlyList<UserSubscriptionResponse>>(userResult.Error);
        }

        var subscriptions = await context.UserSubscriptions
            .AsNoTracking()
            .Where(s => s.UserId == userResult.Value.Id)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

        if (subscriptions.Count == 0)
        {
            return Result.Success<IReadOnlyList<UserSubscriptionResponse>>([]);
        }

        var categoryIds = subscriptions
            .Where(s => s.CategoryId is not null)
            .Select(s => s.CategoryId!.Value)
            .Distinct()
            .ToList();

        var categoryNames = categoryIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await context.EventCategories
                .AsNoTracking()
                .Where(c => categoryIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

        var items = subscriptions
            .Select(s => SubscriptionMapping.Map(
                s,
                s.CategoryId is null
                    ? null
                    : categoryNames.GetValueOrDefault(s.CategoryId.Value)))
            .ToList();

        return items;
    }
}
