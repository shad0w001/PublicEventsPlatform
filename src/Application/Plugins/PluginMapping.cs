using Domain.Plugins;

namespace Application.Plugins;

internal static class PluginMapping
{
    public static IReadOnlyList<EventPluginResponse> ToEventPluginResponses(
        IEnumerable<PluginUsage> usages) =>
        usages
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => ToEventPluginResponse(u, u.Plugin))
            .ToList();

    public static EventPluginResponse ToEventPluginResponse(PluginUsage usage, Plugin catalog) =>
        new(
            catalog.Id,
            catalog.Code,
            catalog.Name,
            usage.Data.ToDictionary(d => d.Key, d => d.Value),
            usage.CreatedAt);
}
