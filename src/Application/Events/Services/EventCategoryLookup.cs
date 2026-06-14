using Application.Abstractions.Data;
using Microsoft.EntityFrameworkCore;

namespace Application.Events.Services;

internal static class EventCategoryLookup
{
    public static Task<string?> ResolveNameAsync(
        IApplicationDbContext context,
        Guid? categoryId,
        CancellationToken cancellationToken)
    {
        if (categoryId is null)
        {
            return Task.FromResult<string?>(null);
        }

        return context.EventCategories
            .AsNoTracking()
            .Where(c => c.Id == categoryId.Value)
            .Select(c => c.Name)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
