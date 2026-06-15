using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Plugins.ListPlugins;

internal sealed class ListPluginsQueryHandler(IApplicationDbContext context)
    : IQueryHandler<ListPluginsQuery, IReadOnlyList<PluginCatalogItemResponse>>
{
    public async Task<Result<IReadOnlyList<PluginCatalogItemResponse>>> Handle(
        ListPluginsQuery query,
        CancellationToken cancellationToken)
    {
        var plugins = await context.Plugins
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => new PluginCatalogItemResponse(
                p.Id,
                p.Code,
                p.Name,
                p.Description,
                p.Version))
            .ToListAsync(cancellationToken);

        return plugins;
    }
}
