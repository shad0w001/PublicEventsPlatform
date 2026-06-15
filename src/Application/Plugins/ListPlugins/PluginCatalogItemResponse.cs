namespace Application.Plugins.ListPlugins;

public sealed record PluginCatalogItemResponse(
    Guid Id,
    string Code,
    string Name,
    string Description,
    string Version);
