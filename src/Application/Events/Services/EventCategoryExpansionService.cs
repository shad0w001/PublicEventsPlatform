using Application.Abstractions.Data;
using Microsoft.EntityFrameworkCore;

namespace Application.Events.Services;

internal static class EventCategoryExpansionService
{
    public static async Task<IReadOnlySet<Guid>> ExpandCategoryIdsAsync(
        IApplicationDbContext context,
        IReadOnlyList<Guid> selectedCategoryIds,
        CancellationToken cancellationToken)
    {
        if (selectedCategoryIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var categories = await context.EventCategories
            .AsNoTracking()
            .Select(c => new CategoryRow(c.Id, c.ParentCategoryId))
            .ToListAsync(cancellationToken);

        var childrenByParent = categories
            .Where(c => c.ParentCategoryId is not null)
            .GroupBy(c => c.ParentCategoryId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(c => c.Id).ToList());

        var expanded = new HashSet<Guid>();
        foreach (var categoryId in selectedCategoryIds.Distinct())
        {
            expanded.Add(categoryId);
            AddDescendants(categoryId, childrenByParent, expanded);
        }

        return expanded;
    }

    private static void AddDescendants(
        Guid parentId,
        IReadOnlyDictionary<Guid, List<Guid>> childrenByParent,
        ISet<Guid> expanded)
    {
        if (!childrenByParent.TryGetValue(parentId, out var children))
        {
            return;
        }

        foreach (var childId in children)
        {
            if (expanded.Add(childId))
            {
                AddDescendants(childId, childrenByParent, expanded);
            }
        }
    }

    private sealed record CategoryRow(Guid Id, Guid? ParentCategoryId);
}
