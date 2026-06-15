using Application.Abstractions.Messaging;

namespace Application.EventCategories.ListEventCategories;

public sealed record ListEventCategoriesQuery : IQuery<IReadOnlyList<EventCategoryTreeNodeResponse>>;
