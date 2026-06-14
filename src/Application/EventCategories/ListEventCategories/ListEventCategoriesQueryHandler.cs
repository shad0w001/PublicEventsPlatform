using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.EventCategories.ListEventCategories;

internal sealed class ListEventCategoriesQueryHandler(IApplicationDbContext context)
    : IQueryHandler<ListEventCategoriesQuery, IReadOnlyList<EventCategoryTreeNodeResponse>>
{
    public async Task<Result<IReadOnlyList<EventCategoryTreeNodeResponse>>> Handle(
        ListEventCategoriesQuery query,
        CancellationToken cancellationToken)
    {
        var categories = await context.EventCategories
            .AsNoTracking()
            .Select(c => new CategoryRow(c.Id, c.Name, c.ParentCategoryId))
            .ToListAsync(cancellationToken);

        var childrenByParent = categories
            .Where(c => c.ParentCategoryId is not null)
            .GroupBy(c => c.ParentCategoryId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(c => c.Name).ToList());

        var roots = categories
            .Where(c => c.ParentCategoryId is null)
            .OrderBy(c => c.Name)
            .Select(c => BuildNode(c, childrenByParent))
            .ToList();

        return roots;
    }

    private static EventCategoryTreeNodeResponse BuildNode(
        CategoryRow row,
        IReadOnlyDictionary<Guid, List<CategoryRow>> childrenByParent)
    {
        if (!childrenByParent.TryGetValue(row.Id, out var children))
        {
            return new EventCategoryTreeNodeResponse(row.Id, row.Name, []);
        }

        var subCategories = children
            .Select(child => BuildNode(child, childrenByParent))
            .ToList();

        return new EventCategoryTreeNodeResponse(row.Id, row.Name, subCategories);
    }

    private sealed record CategoryRow(Guid Id, string Name, Guid? ParentCategoryId);
}
