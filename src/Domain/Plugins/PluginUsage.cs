using Domain.Events;
using SharedKernel;

namespace Domain.Plugins;

public class PluginUsage : Entity
{
    public Guid PluginId { get; internal set; }
    public Guid EventId { get; internal set; }
    public bool IsActive { get; internal set; }
    public List<PluginData> Data { get; internal set; } = [];

    public Plugin Plugin { get; internal set; } = null!;
    public Event Event { get; internal set; } = null!;
}
