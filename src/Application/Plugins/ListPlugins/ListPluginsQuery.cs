using Application.Abstractions.Messaging;

namespace Application.Plugins.ListPlugins;

public sealed record ListPluginsQuery : IQuery<IReadOnlyList<PluginCatalogItemResponse>>;
