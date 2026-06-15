using SharedKernel;

namespace Domain.Plugins;

public class PluginData : Entity
{
    public Guid PluginUsageId { get; internal set; }
    public string Key { get; internal set; } = string.Empty;
    public string? Value { get; internal set; }

    public PluginUsage PluginUsage { get; internal set; } = null!;
}
