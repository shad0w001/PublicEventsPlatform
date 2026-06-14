namespace Application.EventCategories.ListEventCategories;

public sealed record EventCategoryTreeNodeResponse(
    Guid Id,
    string Name,
    IReadOnlyList<EventCategoryTreeNodeResponse> SubCategories);
